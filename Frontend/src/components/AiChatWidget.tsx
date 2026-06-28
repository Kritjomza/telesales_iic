import { FormEvent, KeyboardEvent, useEffect, useRef, useState } from "react";
import { Bot, Loader2, MessageCircle, Send, Sparkles, X } from "lucide-react";
import { aiChatService } from "../domain/aiChatService";
import type { AiChatContext } from "../domain/aiChatService";

type ChatMessage = {
  id: string;
  role: "user" | "assistant";
  text: string;
  source?: string;
};

const quickPrompts = [
  "ขอข้อมูลบริษัท",
  "ขอเบอร์ติดต่อล่าสุด",
  "ขออีเมลล่าสุด",
  "ข้อมูลที่ยังขาด",
  "ลูกค้าใกล้หมดอายุ"
];

const createMessageId = () => `${Date.now()}-${Math.random().toString(36).slice(2, 8)}`;

const getSourceLabel = (source?: string) => {
  if (source === "ai_tool_answer") return "สรุปโดย AI จากข้อมูลในระบบ";
  if (source === "ai_summary") return "สรุปโดย AI จากข้อมูลในระบบ";
  if (source === "database_fallback") return "ใช้คำตอบสำรองจากข้อมูลในระบบ";
  if (source === "database") return "ตอบจากข้อมูลในระบบ";
  return "";
};

export function AiChatWidget() {
  const [isMounted, setIsMounted] = useState(false);
  const [isOpen, setIsOpen] = useState(false);
  const [messages, setMessages] = useState<ChatMessage[]>([]);
  const [draft, setDraft] = useState("");
  const [isSending, setIsSending] = useState(false);
  const [error, setError] = useState("");
  const [lastSelectedCustomer, setLastSelectedCustomer] = useState<AiChatContext | undefined>();
  const closeTimerRef = useRef<number | null>(null);
  const inputRef = useRef<HTMLTextAreaElement | null>(null);
  const messagesEndRef = useRef<HTMLDivElement | null>(null);

  useEffect(() => {
    return () => {
      if (closeTimerRef.current) {
        window.clearTimeout(closeTimerRef.current);
      }
    };
  }, []);

  useEffect(() => {
    if (isOpen) {
      inputRef.current?.focus();
    }
  }, [isOpen]);

  useEffect(() => {
    messagesEndRef.current?.scrollIntoView({ block: "end" });
  }, [messages, isSending]);

  const openPanel = () => {
    if (closeTimerRef.current) {
      window.clearTimeout(closeTimerRef.current);
    }
    setIsMounted(true);
    window.requestAnimationFrame(() => setIsOpen(true));
  };

  const closePanel = () => {
    setIsOpen(false);
    closeTimerRef.current = window.setTimeout(() => {
      setIsMounted(false);
    }, 210);
  };

  const sendMessage = async () => {
    const message = draft.trim();
    if (!message || isSending) return;

    setError("");
    setDraft("");
    setIsSending(true);

    const userMessage: ChatMessage = {
      id: createMessageId(),
      role: "user",
      text: message
    };
    setMessages((current) => [...current, userMessage]);

    try {
      const response = await aiChatService.sendMessage(message, lastSelectedCustomer);
      if (response.metadata?.selectedCustomerId && response.metadata.selectedCustomerName) {
        setLastSelectedCustomer({
          lastSelectedCustomerId: response.metadata.selectedCustomerId,
          lastSelectedCustomerName: response.metadata.selectedCustomerName
        });
      }
      setMessages((current) => [
        ...current,
        {
          id: createMessageId(),
          role: "assistant",
          text: response.reply,
          source: response.metadata?.source
        }
      ]);
    } catch {
      setError("ไม่สามารถเรียกใช้งานผู้ช่วยได้ชั่วคราว");
    } finally {
      setIsSending(false);
    }
  };

  const handleSubmit = (event: FormEvent<HTMLFormElement>) => {
    event.preventDefault();
    void sendMessage();
  };

  const handleKeyDown = (event: KeyboardEvent<HTMLTextAreaElement>) => {
    if (event.key === "Enter" && !event.shiftKey) {
      event.preventDefault();
      void sendMessage();
    }
  };

  return (
    <div className="ai-chat-root">
      {isMounted && (
        <section
          className={`ai-chat-panel ${isOpen ? "open" : "closing"}`}
          role="dialog"
          aria-label="IIC AI Assistant"
          aria-modal="false"
        >
          <header className="ai-chat-header">
            <div className="ai-chat-title-row">
              <span className="ai-chat-status-dot" aria-hidden="true" />
              <div>
                <h2>IIC AI Assistant</h2>
                <p>ผู้ช่วยข้อมูล Telesales</p>
              </div>
            </div>
            <button
              className="ai-chat-icon-button"
              type="button"
              aria-label="Close IIC AI Assistant"
              onClick={closePanel}
            >
              <X size={16} />
            </button>
          </header>

          <div className="ai-chat-messages" aria-live="polite">
            {messages.length === 0 ? (
              <div className="ai-chat-empty">
                <Bot size={22} />
                <strong>Ask about customer information</strong>
                <span>ถามข้อมูลลูกค้า เช่น ขอเบอร์ติดต่อล่าสุดของบริษัท ...</span>
              </div>
            ) : (
              messages.map((message) => (
                <div className={`ai-chat-message ${message.role}`} key={message.id}>
                  {message.role === "assistant" && getSourceLabel(message.source) && (
                    <span className="ai-chat-source-label">{getSourceLabel(message.source)}</span>
                  )}
                  <span>{message.text}</span>
                </div>
              ))
            )}
            {isSending && (
              <div className="ai-chat-loading">
                <Loader2 size={14} />
                <span>Preparing response...</span>
              </div>
            )}
            <div ref={messagesEndRef} aria-hidden="true" />
          </div>

          <div className="ai-chat-prompts" aria-label="Quick prompts">
            {quickPrompts.map((prompt) => (
              <button
                key={prompt}
                type="button"
                className="ai-chat-prompt"
                onClick={() => setDraft(prompt)}
                disabled={isSending}
              >
                {prompt}
              </button>
            ))}
          </div>

          {error && (
            <div className="ai-chat-error" role="alert">
              {error}
            </div>
          )}

          <form className="ai-chat-form" onSubmit={handleSubmit}>
            <textarea
              ref={inputRef}
              aria-label="Message"
              value={draft}
              onChange={(event) => setDraft(event.target.value)}
              onKeyDown={handleKeyDown}
              maxLength={500}
              placeholder="Ask a customer-related question..."
              disabled={isSending}
              rows={1}
            />
            <button
              type="submit"
              className="ai-chat-send"
              aria-label="Send message"
              disabled={!draft.trim() || isSending}
            >
              {isSending ? <Loader2 size={15} /> : <Send size={15} />}
            </button>
          </form>
        </section>
      )}

      <button
        className={`ai-chat-fab ${isOpen ? "active" : ""}`}
        type="button"
        aria-label="Open IIC AI Assistant"
        onClick={isOpen ? closePanel : openPanel}
      >
        <Sparkles size={17} />
        <span>AI</span>
        <MessageCircle size={17} />
      </button>
    </div>
  );
}
