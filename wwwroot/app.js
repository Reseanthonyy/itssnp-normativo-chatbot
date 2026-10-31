// Elementos del Widget Flotante
const floatingWidgetWrapper = document.querySelector(".floating-widget-wrapper");
const btnToggleChat = document.querySelector("#btn-toggle-chat");
const btnClosePopup = document.querySelector("#btn-close-popup");
const btnClearChat = document.querySelector("#btn-clear-chat");
const chatPopup = document.querySelector("#chat-popup");
const popupMessages = document.querySelector("#popup-messages");
const messagesList = document.querySelector("#messages-list");
const popupWelcome = document.querySelector("#popup-welcome");
const popupTyping = document.querySelector("#popup-typing");
const form = document.querySelector("#chat-form");
const input = document.querySelector("#message-input");
const sendButton = document.querySelector("#send-button");
const formError = document.querySelector("#form-error");
const btnMic = document.querySelector("#btn-mic");

// Botones de la Landing Page que abren el Asistente
const btnOpenAssistantNav = document.querySelector("#btn-open-assistant-nav");
const btnHeroAssistant = document.querySelector("#btn-hero-assistant");
const presetChips = document.querySelectorAll(".preset-chip");

let conversationId = null;

// Asegurar que el indicador de consulta esté estrictamente oculto al inicio
if (popupTyping) {
  popupTyping.hidden = true;
}

// Control de apertura y cierre de la burbuja
function openChat() {
  chatPopup.hidden = false;
  floatingWidgetWrapper.classList.add("is-open");
  setTimeout(() => {
    input.focus();
    scrollPopupBottom();
    updatePresetArrows();
  }, 100);
}

function closeChat() {
  chatPopup.hidden = true;
  floatingWidgetWrapper.classList.remove("is-open");
}

function toggleChat() {
  if (chatPopup.hidden) {
    openChat();
  } else {
    closeChat();
  }
}

if (btnToggleChat) btnToggleChat.addEventListener("click", toggleChat);
if (btnClosePopup) btnClosePopup.addEventListener("click", closeChat);
if (btnOpenAssistantNav) btnOpenAssistantNav.addEventListener("click", openChat);
if (btnHeroAssistant) btnHeroAssistant.addEventListener("click", openChat);

// Controles de Carrusel y Arrastre para Preguntas Frecuentes
const presetsScroll = document.querySelector("#presets-scroll");
const btnScrollLeft = document.querySelector("#preset-scroll-left");
const btnScrollRight = document.querySelector("#preset-scroll-right");

let isPresetDragging = false;
let presetStartX = 0;
let presetScrollStart = 0;
let hasDraggedPreset = false;

function updatePresetArrows() {
  if (!presetsScroll || !btnScrollLeft || !btnScrollRight) return;
  const maxScroll = presetsScroll.scrollWidth - presetsScroll.clientWidth;
  btnScrollLeft.disabled = presetsScroll.scrollLeft <= 3;
  btnScrollRight.disabled = presetsScroll.scrollLeft >= maxScroll - 3;
}

if (presetsScroll) {
  // Conversión de rueda vertical a desplazamiento horizontal suave
  presetsScroll.addEventListener("wheel", (e) => {
    if (e.deltaY !== 0) {
      e.preventDefault();
      presetsScroll.scrollLeft += e.deltaY;
      updatePresetArrows();
    }
  }, { passive: false });

  // Arrastre con ratón (Drag to scroll)
  presetsScroll.addEventListener("mousedown", (e) => {
    isPresetDragging = true;
    hasDraggedPreset = false;
    presetsScroll.classList.add("is-dragging");
    presetStartX = e.pageX - presetsScroll.offsetLeft;
    presetScrollStart = presetsScroll.scrollLeft;
  });

  window.addEventListener("mouseup", () => {
    if (isPresetDragging) {
      isPresetDragging = false;
      presetsScroll.classList.remove("is-dragging");
      setTimeout(() => { hasDraggedPreset = false; }, 80);
    }
  });

  presetsScroll.addEventListener("mousemove", (e) => {
    if (!isPresetDragging) return;
    const x = e.pageX - presetsScroll.offsetLeft;
    const walk = (x - presetStartX) * 1.4;
    if (Math.abs(walk) > 4) {
      hasDraggedPreset = true;
    }
    presetsScroll.scrollLeft = presetScrollStart - walk;
    updatePresetArrows();
  });

  presetsScroll.addEventListener("scroll", updatePresetArrows, { passive: true });

  if (btnScrollLeft) {
    btnScrollLeft.addEventListener("click", (e) => {
      e.preventDefault();
      presetsScroll.scrollBy({ left: -200, behavior: "smooth" });
      setTimeout(updatePresetArrows, 260);
    });
  }

  if (btnScrollRight) {
    btnScrollRight.addEventListener("click", (e) => {
      e.preventDefault();
      presetsScroll.scrollBy({ left: 200, behavior: "smooth" });
      setTimeout(updatePresetArrows, 260);
    });
  }

  // Inicialización de estado de flechas
  setTimeout(updatePresetArrows, 150);
}

// Chips de preguntas sugeridas dentro del Chatbot
presetChips.forEach((chip) => {
  chip.addEventListener("click", () => {
    if (hasDraggedPreset) {
      return; // El usuario estaba arrastrando; ignorar clic accidental
    }
    const question = chip.getAttribute("data-question");
    if (question) {
      handleSendMessage(question);
    }
  });
});

// Formateo seguro: elimina tokens internos de Foundry y crea HTML estructurado
function renderFormattedText(text) {
  const container = document.createElement("div");
  container.className = "msg-text";

  const cleanedText = text.replace(/\ue200[^\ue201]*\ue201/g, "").trim();
  const lines = cleanedText.split("\n");

  let currentList = null;
  let currentParagraph = [];

  function flushParagraph() {
    if (currentParagraph.length > 0) {
      const p = document.createElement("p");
      p.innerHTML = formatInline(currentParagraph.join(" "));
      container.appendChild(p);
      currentParagraph = [];
    }
  }

  function flushList() {
    if (currentList) {
      container.appendChild(currentList);
      currentList = null;
    }
  }

  for (const rawLine of lines) {
    const line = rawLine.trim();

    if (!line) {
      flushParagraph();
      flushList();
      continue;
    }

    if (line.startsWith("- ") || line.startsWith("* ")) {
      flushParagraph();
      if (!currentList || currentList.tagName !== "UL") {
        flushList();
        currentList = document.createElement("ul");
      }
      const li = document.createElement("li");
      li.innerHTML = formatInline(line.substring(2).trim());
      currentList.appendChild(li);
      continue;
    }

    const numMatch = line.match(/^(\d+[\.\)])\s+(.*)$/);
    if (numMatch) {
      flushParagraph();
      if (!currentList || currentList.tagName !== "OL") {
        flushList();
        currentList = document.createElement("ol");
      }
      const li = document.createElement("li");
      li.innerHTML = formatInline(numMatch[2].trim());
      currentList.appendChild(li);
      continue;
    }

    flushList();
    currentParagraph.push(line);
  }

  flushParagraph();
  flushList();

  return container;
}

function formatInline(str) {
  const escaped = str
    .replace(/&/g, "&amp;")
    .replace(/</g, "&lt;")
    .replace(/>/g, "&gt;")
    .replace(/"/g, "&quot;")
    .replace(/'/g, "&#039;");

  return escaped.replace(/\*\*(.*?)\*\*/g, "<strong>$1</strong>");
}

function scrollPopupBottom() {
  if (popupMessages) {
    popupMessages.scrollTop = popupMessages.scrollHeight;
  }
}

function renderCitationsUnderBubble(bubble, citations) {
  if (!bubble || !citations || citations.length === 0) return;
  if (bubble.querySelector(".msg-citations")) return;

  const citationsWrap = document.createElement("div");
  citationsWrap.className = "msg-citations";

  const label = document.createElement("span");
  label.className = "citations-label";
  label.innerHTML = `<svg viewBox="0 0 24 24" width="13" height="13" fill="none" stroke="currentColor" stroke-width="2" style="vertical-align:-1px; margin-right:4px;"><path d="M4 19.5A2.5 2.5 0 0 1 6.5 17H20"></path><path d="M6.5 2H20v20H6.5A2.5 2.5 0 0 1 4 19.5v-15A2.5 2.5 0 0 1 6.5 2z"></path></svg> Fuentes normativas consultadas (RAG):`;
  citationsWrap.appendChild(label);

  const list = document.createElement("div");
  list.className = "citations-list";

  citations.forEach(fn => {
    const cleanName = fn
      .replace(/^\d+[-_]/, "")
      .replace(/\.pdf$/i, "")
      .replaceAll("_", " ");
    const pill = document.createElement("span");
    pill.className = "citation-pill";
    pill.title = `Documento indexado oficial en Azure AI Foundry: ${fn}`;
    pill.innerHTML = `<svg viewBox="0 0 24 24" width="11" height="11" fill="none" stroke="currentColor" stroke-width="2" style="vertical-align:-1px; margin-right:3px;"><path d="M14 2H6a2 2 0 0 0-2 2v16a2 2 0 0 0 2 2h12a2 2 0 0 0 2-2V8z"></path><polyline points="14 2 14 8 20 8"></polyline></svg><span>${cleanName}</span>`;
    list.appendChild(pill);
  });

  citationsWrap.appendChild(list);
  bubble.appendChild(citationsWrap);
}

// Barra de Retroalimentación de Calidad (👍 / 👎)
function renderFeedbackBar(bubble, queryId, convId) {
  if (!bubble) return;
  if (bubble.querySelector(".msg-feedback-bar")) return;

  const bar = document.createElement("div");
  bar.className = "msg-feedback-bar";

  const label = document.createElement("span");
  label.className = "feedback-label";
  label.textContent = "¿Te fue útil esta orientación?";
  bar.appendChild(label);

  const btnLike = document.createElement("button");
  btnLike.type = "button";
  btnLike.className = "btn-feedback-action btn-like";
  btnLike.innerHTML = `<svg viewBox="0 0 24 24" width="12" height="12" fill="none" stroke="currentColor" stroke-width="2" style="vertical-align:-1px; margin-right:3px;"><path d="M14 9V5a3 3 0 0 0-3-3l-4 9v11h11.28a2 2 0 0 0 2-1.7l1.38-9a2 2 0 0 0-2-2.3zM7 22H4a2 2 0 0 1-2-2v-7a2 2 0 0 1 2-2h3"></path></svg><span>Sí</span>`;
  btnLike.title = "Respuesta clara y fundamentada";

  const btnDislike = document.createElement("button");
  btnDislike.type = "button";
  btnDislike.className = "btn-feedback-action btn-dislike";
  btnDislike.innerHTML = `<svg viewBox="0 0 24 24" width="12" height="12" fill="none" stroke="currentColor" stroke-width="2" style="vertical-align:-1px; margin-right:3px;"><path d="M10 15v4a3 3 0 0 0 3 3l4-9V2H5.72a2 2 0 0 0-2 1.7l-1.38 9a2 2 0 0 0 2 2.3zm7-13h3a2 2 0 0 1 2 2v7a2 2 0 0 1-2 2h-3"></path></svg><span>No</span>`;
  btnDislike.title = "No resolvió mi duda";

  async function sendRating(val, clickedBtn, otherBtn) {
    clickedBtn.disabled = true;
    otherBtn.disabled = true;
    clickedBtn.classList.add("is-selected");

    try {
      const payload = {
        query_id: queryId || null,
        conversation_id: convId || conversationId || null,
        rating: val
      };

      await fetch("/api/feedback", {
        method: "POST",
        headers: { "Content-Type": "application/json" },
        body: JSON.stringify(payload)
      });

      const confirmed = document.createElement("span");
      confirmed.className = "feedback-confirmed-text";
      confirmed.textContent = "✓ ¡Gracias por tu opinión!";
      bar.replaceChildren(confirmed);
    } catch (e) {
      console.warn("Feedback error", e);
    }
  }

  btnLike.addEventListener("click", (e) => {
    e.preventDefault();
    e.stopPropagation();
    sendRating(1, btnLike, btnDislike);
  });

  btnDislike.addEventListener("click", (e) => {
    e.preventDefault();
    e.stopPropagation();
    sendRating(-1, btnDislike, btnLike);
  });

  bar.appendChild(btnLike);
  bar.appendChild(btnDislike);
  bubble.appendChild(bar);
}

function appendMessage(text, sender, citations = null, queryId = null, convId = null) {
  if (popupWelcome) {
    popupWelcome.hidden = true;
  }

  const row = document.createElement("article");
  row.className = `msg-row ${sender}`;

  const bubble = document.createElement("div");
  bubble.className = "msg-bubble";

  if (sender === "bot") {
    const tag = document.createElement("div");
    tag.className = "msg-tag";
    tag.textContent = "ITSSNP · Asistente Normativo";
    bubble.appendChild(tag);
    bubble.appendChild(renderFormattedText(text));
    renderCitationsUnderBubble(bubble, citations);
    renderFeedbackBar(bubble, queryId, convId || conversationId);
  } else {
    const userText = document.createElement("p");
    userText.textContent = text;
    bubble.appendChild(userText);
  }

  row.appendChild(bubble);
  messagesList.appendChild(row);

  scrollPopupBottom();
  return bubble;
}

async function handleSendMessage(message) {
  formError.hidden = true;

  if (!message) {
    formError.textContent = "Escribe una consulta antes de enviar.";
    formError.hidden = false;
    input.focus();
    return;
  }

  if (message.length > 2000) {
    formError.textContent = "El mensaje no puede superar los 2000 caracteres.";
    formError.hidden = false;
    input.focus();
    return;
  }

  appendMessage(message, "user");
  input.value = "";
  input.style.height = "auto";
  sendButton.disabled = true;

  // Mostrar el indicador de consulta mientras esperamos respuesta
  popupTyping.hidden = false;
  scrollPopupBottom();

  try {
    const response = await fetch("/api/chat/stream", {
      method: "POST",
      headers: { "Content-Type": "application/json" },
      body: JSON.stringify({
        message,
        conversation_id: conversationId,
      }),
    });

    if (!response.ok) {
      const errData = await response.json().catch(() => ({}));
      throw new Error(errData.detail || errData.error || "No fue posible procesar la consulta.");
    }

    const reader = response.body.getReader();
    const decoder = new TextDecoder("utf-8");
    let accumulatedText = "";
    let finalCitations = [];
    let savedQueryId = null;
    let botBubble = null;
    let botTextElement = null;
    let buffer = "";

    while (true) {
      const { done, value } = await reader.read();
      if (done) break;

      buffer += decoder.decode(value, { stream: true });
      const parts = buffer.split("\n\n");
      buffer = parts.pop() || "";

      for (const part of parts) {
        if (!part.startsWith("data: ")) continue;
        const jsonStr = part.slice(6).trim();
        if (!jsonStr) continue;

        try {
          const chunk = JSON.parse(jsonStr);

          if (chunk.type === "delta" && chunk.content) {
            popupTyping.hidden = true;

            if (!botBubble) {
              const row = document.createElement("article");
              row.className = "msg-row bot";
              botBubble = document.createElement("div");
              botBubble.className = "msg-bubble";

              const tag = document.createElement("div");
              tag.className = "msg-tag";
              tag.textContent = "ITSSNP · Asistente Normativo";
              botBubble.appendChild(tag);

              botTextElement = document.createElement("div");
              botBubble.appendChild(botTextElement);

              row.appendChild(botBubble);
              messagesList.appendChild(row);
            }

            accumulatedText += chunk.content;
            botTextElement.innerHTML = "";
            botTextElement.appendChild(renderFormattedText(accumulatedText));
            scrollPopupBottom();
          } else if (chunk.type === "done") {
            popupTyping.hidden = true;

            if (chunk.conversation_id) {
              conversationId = chunk.conversation_id;
            }
            if (chunk.query_id) {
              savedQueryId = chunk.query_id;
            }
            if (chunk.citations && chunk.citations.length > 0) {
              finalCitations = chunk.citations;
              renderCitationsUnderBubble(botBubble, finalCitations);
            }

            // Renderizar la barra de feedback una vez finalizada la respuesta
            renderFeedbackBar(botBubble, savedQueryId, chunk.conversation_id || conversationId);
            scrollPopupBottom();
          } else if (chunk.type === "error") {
            throw new Error(chunk.content || "Error en el flujo del asistente");
          }
        } catch (e) {
          console.warn("Parse SSE chunk error", e);
        }
      }
    }

    if (!botBubble && accumulatedText) {
      appendMessage(accumulatedText, "bot", finalCitations, savedQueryId, conversationId);
    }
  } catch (error) {
    appendMessage(
      `Disculpa, ocurrió un error al consultar la base normativa institucional: ${error.message}`,
      "bot"
    );
  } finally {
    popupTyping.hidden = true;
    sendButton.disabled = false;
    input.focus();
    scrollPopupBottom();
  }
}

// ==========================================
// Integración de Voz: Azure Speech & Web Speech
// ==========================================
let speechRecognition = null;
let isListening = false;
let mediaRecorder = null;
let audioChunks = [];

const SpeechRecognitionClass = window.SpeechRecognition || window.webkitSpeechRecognition;
if (SpeechRecognitionClass) {
  speechRecognition = new SpeechRecognitionClass();
  speechRecognition.lang = "es-MX";
  speechRecognition.continuous = false;
  speechRecognition.interimResults = true;

  speechRecognition.onstart = () => {
    isListening = true;
    if (btnMic) btnMic.classList.add("is-listening");
    input.placeholder = "Escuchando tu voz... (habla con claridad)";
  };

  speechRecognition.onresult = (event) => {
    let transcript = "";
    for (let i = event.resultIndex; i < event.results.length; ++i) {
      transcript += event.results[i][0].transcript;
    }
    if (transcript) {
      input.value = transcript;
      input.style.height = "auto";
      input.style.height = `${Math.min(input.scrollHeight, 90)}px`;
    }
  };

  speechRecognition.onerror = (event) => {
    console.warn("Speech recognition error:", event.error);
    stopListening();
  };

  speechRecognition.onend = () => {
    stopListening();
    if (input.value.trim().length > 0) {
      form.requestSubmit();
    }
  };
}

function stopListening() {
  isListening = false;
  if (btnMic) btnMic.classList.remove("is-listening");
  input.placeholder = "Escribe tu consulta normativa… (Enter para enviar)";
  if (speechRecognition) {
    try { speechRecognition.stop(); } catch {}
  }
  if (mediaRecorder && mediaRecorder.state === "recording") {
    try { mediaRecorder.stop(); } catch {}
  }
}

async function startListening() {
  formError.hidden = true;

  if (speechRecognition) {
    try {
      speechRecognition.start();
      return;
    } catch (e) {
      console.warn("Reintentando con captura de audio para Azure Speech", e);
    }
  }

  // Fallback: Captura mediante MediaRecorder y subida a Azure AI Speech
  if (navigator.mediaDevices && navigator.mediaDevices.getUserMedia) {
    try {
      const stream = await navigator.mediaDevices.getUserMedia({ audio: true });
      audioChunks = [];
      mediaRecorder = new MediaRecorder(stream);

      mediaRecorder.ondataavailable = (e) => {
        if (e.data && e.data.size > 0) {
          audioChunks.push(e.data);
        }
      };

      mediaRecorder.onstop = async () => {
        stream.getTracks().forEach(t => t.stop());
        if (audioChunks.length > 0) {
          const audioBlob = new Blob(audioChunks, { type: "audio/wav" });
          await sendAudioToAzureSpeech(audioBlob);
        }
      };

      mediaRecorder.start();
      isListening = true;
      if (btnMic) btnMic.classList.add("is-listening");
      input.placeholder = "Grabando audio para Azure AI Speech... Presiona de nuevo el micro para enviar";
    } catch (err) {
      formError.textContent = "No fue posible acceder al micrófono: " + err.message;
      formError.hidden = false;
      stopListening();
    }
  } else {
    formError.textContent = "Tu navegador no cuenta con soporte de micrófono.";
    formError.hidden = false;
  }
}

async function sendAudioToAzureSpeech(blob) {
  try {
    popupTyping.hidden = false;
    const formData = new FormData();
    formData.append("audio", blob, "voice.wav");

    const res = await fetch("/api/speech/transcribe", {
      method: "POST",
      body: formData
    });

    const data = await res.json();
    if (data.success && data.text) {
      input.value = data.text;
      form.requestSubmit();
    } else {
      formError.textContent = data.error || "No se detectó voz clara en el audio.";
      formError.hidden = false;
    }
  } catch (err) {
    console.error("Azure speech upload failed", err);
  } finally {
    stopListening();
    popupTyping.hidden = true;
  }
}

if (btnMic) {
  btnMic.addEventListener("click", () => {
    if (isListening) {
      stopListening();
    } else {
      startListening();
    }
  });
}

// Envío de formulario
form.addEventListener("submit", (event) => {
  event.preventDefault();
  handleSendMessage(input.value.trim());
});

// Auto-ajuste de altura del textarea y Enter para enviar
input.addEventListener("input", () => {
  input.style.height = "auto";
  input.style.height = `${Math.min(input.scrollHeight, 90)}px`;
  formError.hidden = true;
});

input.addEventListener("keydown", (event) => {
  if (event.key === "Enter" && !event.shiftKey) {
    event.preventDefault();
    form.requestSubmit();
  }
});

// Reiniciar conversación
btnClearChat.addEventListener("click", () => {
  conversationId = null;
  messagesList.innerHTML = "";
  if (popupWelcome) {
    popupWelcome.hidden = false;
  }
  popupTyping.hidden = true;
  input.value = "";
  input.style.height = "auto";
  input.focus();
});
