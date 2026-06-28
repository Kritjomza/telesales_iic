import { ApiError } from "./apiService";

const API_BASE = import.meta.env.VITE_API_BASE_URL || "/api";

export type AiChatResponse = {
  reply: string;
  metadata?: {
    source: "ai_tool_answer" | "ai_summary" | "database_fallback" | "database" | "blocked" | "error" | string;
    usedAi: boolean;
    matchedCustomersCount: number;
    selectedCustomerId?: number;
    selectedCustomerName?: string;
    intent?: string;
  };
};

export type AiChatContext = {
  lastSelectedCustomerId: number;
  lastSelectedCustomerName: string;
};

export const aiChatService = {
  async sendMessage(message: string, context?: AiChatContext): Promise<AiChatResponse> {
    const response = await fetch(`${API_BASE}/ai-chat`, {
      method: "POST",
      credentials: "same-origin",
      headers: {
        "Content-Type": "application/json"
      },
      body: JSON.stringify({
        message,
        lastSelectedCustomerId: context?.lastSelectedCustomerId,
        lastSelectedCustomerName: context?.lastSelectedCustomerName
      })
    });

    if (!response.ok) {
      let errorMessage = "ไม่สามารถเรียกใช้งานผู้ช่วยได้ชั่วคราว";
      try {
        const text = await response.text();
        if (text) {
          const parsed = JSON.parse(text);
          if (typeof parsed.message === "string" && parsed.message.length <= 120) {
            errorMessage = parsed.message;
          }
        }
      } catch {
        errorMessage = "ไม่สามารถเรียกใช้งานผู้ช่วยได้ชั่วคราว";
      }

      throw new ApiError(response.status, errorMessage);
    }

    return response.json();
  }
};
