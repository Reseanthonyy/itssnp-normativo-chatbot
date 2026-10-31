document.addEventListener("DOMContentLoaded", () => {
  const gateSection = document.getElementById("admin-gate");
  const dashboardSection = document.getElementById("admin-dashboard");
  const gateForm = document.getElementById("gate-form");
  const gatePinInput = document.getElementById("gate-pin");

  const kpiQueries = document.getElementById("kpi-queries");
  const kpiRated = document.getElementById("kpi-rated");
  const kpiSatisfaction = document.getElementById("kpi-satisfaction");
  const kpiOpinions = document.getElementById("kpi-opinions");

  const queriesTableBody = document.getElementById("queries-table-body");
  const filterRating = document.getElementById("filter-rating");
  const btnRefreshQueries = document.getElementById("btn-refresh-queries");

  const tabBtns = document.querySelectorAll(".tab-btn");
  const tabPanes = document.querySelectorAll(".tab-pane");

  function checkAuth() {
    if (sessionStorage.getItem("itssnp_admin_auth") === "true") {
      gateSection.hidden = true;
      dashboardSection.hidden = false;
      loadAllData();
    } else {
      gateSection.hidden = false;
      dashboardSection.hidden = true;
    }
  }

  gateForm.addEventListener("submit", (e) => {
    e.preventDefault();
    const pin = gatePinInput.value.trim();
    if (pin === "2026" || pin === "admin") {
      sessionStorage.setItem("itssnp_admin_auth", "true");
      checkAuth();
    } else {
      alert("Clave de acceso incorrecta. Utilice la clave autorizada: 2026");
    }
  });

  tabBtns.forEach((btn) => {
    btn.addEventListener("click", () => {
      tabBtns.forEach((b) => b.classList.remove("active"));
      tabPanes.forEach((p) => p.classList.remove("active"));

      btn.classList.add("active");
      const targetId = btn.getAttribute("data-tab");
      const targetPane = document.getElementById(targetId);
      if (targetPane) targetPane.classList.add("active");
    });
  });

  async function loadAllData() {
    await loadMetrics();
    await loadQueries();
  }

  async function loadMetrics() {
    try {
      const res = await fetch("/api/admin/metrics");
      if (res.ok) {
        const data = await res.json();
        if (kpiQueries) kpiQueries.textContent = data.total_queries;
        if (kpiRated) kpiRated.textContent = data.rated_queries;
        if (kpiSatisfaction) kpiSatisfaction.textContent = `${data.satisfaction_rate}%`;
        if (kpiOpinions) kpiOpinions.innerHTML = `<span style="color:#059669; font-weight:700;">+${data.positive_ratings}</span> <span style="color:#94A3B8; font-weight:400; font-size:1.1rem;">/</span> <span style="color:#DC2626; font-weight:700;">-${data.negative_ratings}</span>`;
      }
    } catch (err) {
      console.error("Error al cargar KPIs", err);
    }
  }

  async function loadQueries() {
    try {
      const rating = filterRating ? filterRating.value : "";
      const url = rating ? `/api/admin/queries?rating=${encodeURIComponent(rating)}` : "/api/admin/queries";
      const res = await fetch(url);
      if (res.ok) {
        const queries = await res.json();
        renderQueries(queries);
      }
    } catch (err) {
      console.error("Error al cargar bitácora", err);
    }
  }

  function renderQueries(queries) {
    if (!queriesTableBody) return;
    if (queries.length === 0) {
      queriesTableBody.innerHTML = `<tr><td colspan="5" style="text-align:center; color:#64748B; padding:20px;">No hay consultas con el criterio seleccionado.</td></tr>`;
      return;
    }

    queriesTableBody.innerHTML = queries.map(q => {
      const date = new Date(q.createdAt).toLocaleDateString("es-MX", { day: "2-digit", month: "short", hour: "2-digit", minute: "2-digit" });
      let citationsBadge = `<span style="color:#94A3B8;">—</span>`;
      if (q.citationsJson) {
        try {
          const list = JSON.parse(q.citationsJson);
          if (Array.isArray(list) && list.length > 0) {
            citationsBadge = list.map(c => {
              const clean = c.replace(/^\d+[-_]/, '').replace(/\.pdf$/i, '').replaceAll('_', ' ');
              return `<span style="display:inline-flex; align-items:center; gap:3px; font-size:0.72rem; font-weight:600; background:rgba(27,57,106,0.08); color:#1B396A; border:1px solid rgba(27,57,106,0.2); padding:2px 7px; border-radius:12px; margin:2px 3px 2px 0;"><svg viewBox="0 0 24 24" width="10" height="10" fill="none" stroke="currentColor" stroke-width="2"><path d="M14 2H6a2 2 0 0 0-2 2v16a2 2 0 0 0 2 2h12a2 2 0 0 0 2-2V8z"></path><polyline points="14 2 14 8 20 8"></polyline></svg> ${escapeHtml(clean)}</span>`;
            }).join("");
          }
        } catch { }
      }

      let ratingBadge = `<span style="color:#94A3B8; font-size:0.8rem;">Sin calificar</span>`;
      if (q.rating === 1) {
        ratingBadge = `<span style="color:#059669; font-weight:700; display:inline-flex; align-items:center; gap:4px; font-size:0.82rem;"><svg viewBox="0 0 24 24" width="13" height="13" fill="none" stroke="currentColor" stroke-width="2"><path d="M14 9V5a3 3 0 0 0-3-3l-4 9v11h11.28a2 2 0 0 0 2-1.7l1.38-9a2 2 0 0 0-2-2.3zM7 22H4a2 2 0 0 1-2-2v-7a2 2 0 0 1 2-2h3"></path></svg> Útil</span>`;
      } else if (q.rating === -1) {
        ratingBadge = `<span style="color:#DC2626; font-weight:700; display:inline-flex; align-items:center; gap:4px; font-size:0.82rem;"><svg viewBox="0 0 24 24" width="13" height="13" fill="none" stroke="currentColor" stroke-width="2"><path d="M10 15v4a3 3 0 0 0 3 3l4-9V2H5.72a2 2 0 0 0-2 1.7l-1.38 9a2 2 0 0 0 2 2.3zm7-13h3a2 2 0 0 1 2 2v7a2 2 0 0 1-2 2h-3"></path></svg> No resolvió</span>`;
      }

      return `
        <tr>
          <td style="white-space:nowrap; font-size:0.82rem; color:#475569;">${date}</td>
          <td style="max-width:320px; line-height:1.4;"><strong>${escapeHtml(q.userMessage)}</strong></td>
          <td><span style="font-weight:600; color:#1B396A; font-size:0.82rem;">${escapeHtml(q.topic)}</span></td>
          <td style="max-width:280px;">${citationsBadge}</td>
          <td>${ratingBadge}</td>
        </tr>
      `;
    }).join("");
  }

  if (filterRating) {
    filterRating.addEventListener("change", loadQueries);
  }

  if (btnRefreshQueries) {
    btnRefreshQueries.addEventListener("click", async () => {
      await loadMetrics();
      await loadQueries();
    });
  }

  function escapeHtml(text) {
    if (!text) return "";
    return text.toString()
      .replace(/&/g, "&amp;")
      .replace(/</g, "&lt;")
      .replace(/>/g, "&gt;")
      .replace(/"/g, "&quot;")
      .replace(/'/g, "&#039;");
  }

  checkAuth();
});
