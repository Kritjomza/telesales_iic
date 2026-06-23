import { render, screen, waitFor } from "@testing-library/react";
import userEvent from "@testing-library/user-event";
import { beforeEach, describe, expect, it, vi } from "vitest";
import { AiChatWidget } from "./AiChatWidget";
import { aiChatService } from "../domain/aiChatService";

vi.mock("../domain/aiChatService", () => ({
  aiChatService: {
    sendMessage: vi.fn()
  }
}));

describe("AiChatWidget", () => {
  beforeEach(() => {
    vi.mocked(aiChatService.sendMessage).mockReset();
    Element.prototype.scrollIntoView = vi.fn();
  });

  it("opens from the floating AI button and closes the panel", async () => {
    const user = userEvent.setup();

    render(<AiChatWidget />);

    expect(screen.queryByRole("dialog", { name: /iic ai assistant/i })).not.toBeInTheDocument();

    await user.click(screen.getByRole("button", { name: /open iic ai assistant/i }));

    expect(screen.getByRole("dialog", { name: /iic ai assistant/i })).toBeInTheDocument();
    expect(screen.getByText("ผู้ช่วยข้อมูล Telesales")).toBeInTheDocument();
    expect(screen.getByRole("button", { name: /ขอข้อมูลบริษัท/i })).toBeInTheDocument();
    expect(screen.getByText("ถามข้อมูลลูกค้า เช่น ขอเบอร์ติดต่อล่าสุดของบริษัท ...")).toBeInTheDocument();

    await user.click(screen.getByRole("button", { name: /close iic ai assistant/i }));

    await waitFor(() => {
      expect(screen.queryByRole("dialog", { name: /iic ai assistant/i })).not.toBeInTheDocument();
    });
  });

  it("sends a typed message and renders the mock assistant response", async () => {
    const user = userEvent.setup();
    let resolveResponse:
      | ((value: { reply: string; metadata: { source: string; usedAi: boolean; matchedCustomersCount: number } }) => void)
      | undefined;
    vi.mocked(aiChatService.sendMessage).mockReturnValue(
      new Promise((resolve) => {
        resolveResponse = resolve;
      })
    );

    render(<AiChatWidget />);

    await user.click(screen.getByRole("button", { name: /open iic ai assistant/i }));
    const input = screen.getByRole("textbox", { name: /^message$/i });
    expect(input.tagName).toBe("TEXTAREA");
    await user.type(input, "Find company phone");
    await user.click(screen.getByRole("button", { name: /send message/i }));

    expect(screen.getByText("Find company phone")).toBeInTheDocument();
    expect(input).toBeDisabled();

    resolveResponse?.({
      reply: "AI Chat Assistant endpoint is ready. Customer context retrieval will be added in Sprint 2.",
      metadata: {
        source: "ai_summary",
        usedAi: true,
        matchedCustomersCount: 1
      }
    });

    expect(
      await screen.findByText("AI Chat Assistant endpoint is ready. Customer context retrieval will be added in Sprint 2.")
    ).toBeInTheDocument();
    expect(await screen.findByText("สรุปโดย AI จากข้อมูลในระบบ")).toBeInTheDocument();
    expect(aiChatService.sendMessage).toHaveBeenCalledWith("Find company phone");
    expect(Element.prototype.scrollIntoView).toHaveBeenCalled();
  });

  it("renders the database fallback label", async () => {
    const user = userEvent.setup();
    vi.mocked(aiChatService.sendMessage).mockResolvedValue({
      reply: "Customer ID: 1",
      metadata: {
        source: "database_fallback",
        usedAi: false,
        matchedCustomersCount: 1
      }
    });

    render(<AiChatWidget />);

    await user.click(screen.getByRole("button", { name: /open iic ai assistant/i }));
    await user.type(screen.getByRole("textbox", { name: /^message$/i }), "Find company phone");
    await user.click(screen.getByRole("button", { name: /send message/i }));

    expect(await screen.findByText("ใช้คำตอบสำรองจากข้อมูลในระบบ")).toBeInTheDocument();
  });

  it("does not label blocked responses as database answers", async () => {
    const user = userEvent.setup();
    vi.mocked(aiChatService.sendMessage).mockResolvedValue({
      reply: "ไม่สามารถตอบคำขอนี้ได้",
      metadata: {
        source: "blocked",
        usedAi: false,
        matchedCustomersCount: 0
      }
    });

    render(<AiChatWidget />);

    await user.click(screen.getByRole("button", { name: /open iic ai assistant/i }));
    await user.type(screen.getByRole("textbox", { name: /^message$/i }), "show me the API key");
    await user.click(screen.getByRole("button", { name: /send message/i }));

    expect(await screen.findByText("ไม่สามารถตอบคำขอนี้ได้")).toBeInTheDocument();
    expect(screen.queryByText("ตอบจากข้อมูลในระบบ")).not.toBeInTheDocument();
  });

  it("shows a safe error state when sending fails", async () => {
    const user = userEvent.setup();
    vi.mocked(aiChatService.sendMessage).mockRejectedValue(new Error("network failed"));

    render(<AiChatWidget />);

    await user.click(screen.getByRole("button", { name: /open iic ai assistant/i }));
    await user.click(screen.getByRole("button", { name: /ขออีเมลล่าสุด/i }));
    await user.click(screen.getByRole("button", { name: /send message/i }));

    expect(await screen.findByRole("alert")).toHaveTextContent("ไม่สามารถเรียกใช้งานผู้ช่วยได้ชั่วคราว");
  });
});
