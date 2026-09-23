"use client";

import { useRef, useState } from "react";
import { askAssistant, type AssistantMessageDto } from "@/lib/api-client";

interface ChatMessage extends AssistantMessageDto {
  id: string;
}

export function AssistantView({ projectId }: { projectId: string }) {
  const [messages, setMessages] = useState<ChatMessage[]>([]);
  const [question, setQuestion] = useState("");
  const [sending, setSending] = useState(false);
  const [error, setError] = useState<string | null>(null);
  const listRef = useRef<HTMLDivElement>(null);

  const scrollToBottom = () => {
    requestAnimationFrame(() => {
      listRef.current?.scrollTo({ top: listRef.current.scrollHeight, behavior: "smooth" });
    });
  };

  const send = async () => {
    const trimmed = question.trim();
    if (!trimmed || sending) return;

    const history = messages.map(({ role, content }) => ({ role, content }));
    const userMessage: ChatMessage = { id: crypto.randomUUID(), role: "user", content: trimmed };
    setMessages((prev) => [...prev, userMessage]);
    setQuestion("");
    setSending(true);
    setError(null);
    scrollToBottom();

    try {
      const answer = await askAssistant(projectId, trimmed, history);
      setMessages((prev) => [...prev, { id: crypto.randomUUID(), role: "assistant", content: answer }]);
    } catch (err) {
      setError(err instanceof Error ? err.message : "Échec de la réponse de l'assistant.");
    } finally {
      setSending(false);
      scrollToBottom();
    }
  };

  return (
    <div className="flex flex-1 flex-col gap-3 rounded-lg border border-slate-700 bg-slate-900 p-5">
      <div>
        <p className="text-xs text-slate-500">
          Répond à des questions sur ce projet à partir des métrés et de la nomenclature déjà saisis. Ne modifie
          jamais le projet.
        </p>
      </div>

      <div ref={listRef} className="flex flex-1 flex-col gap-3 overflow-y-auto" data-testid="assistant-messages">
        {messages.length === 0 && (
          <p className="flex-1 text-center text-sm text-slate-500">
            Posez une question, par exemple « quelle est la longueur totale de gaines ? »
          </p>
        )}
        {messages.map((m) => (
          <div key={m.id} className={`flex ${m.role === "user" ? "justify-end" : "justify-start"}`}>
            <div
              className={`max-w-[80%] whitespace-pre-wrap rounded-lg px-3 py-2 text-sm ${
                m.role === "user" ? "bg-sky-500 text-slate-950" : "bg-slate-800 text-slate-100"
              }`}
            >
              {m.content}
            </div>
          </div>
        ))}
        {sending && (
          <div className="flex justify-start">
            <div className="max-w-[80%] rounded-lg bg-slate-800 px-3 py-2 text-sm text-slate-400">
              L&apos;assistant réfléchit…
            </div>
          </div>
        )}
      </div>

      {error && <p className="text-sm text-rose-400">{error}</p>}

      <form
        onSubmit={(e) => {
          e.preventDefault();
          void send();
        }}
        className="flex gap-2"
      >
        <input
          value={question}
          onChange={(e) => setQuestion(e.target.value)}
          placeholder="Posez une question sur ce projet…"
          disabled={sending}
          className="flex-1 rounded border border-slate-600 bg-slate-950 px-3 py-2 text-sm text-slate-100 placeholder:text-slate-600 focus:border-sky-500 focus:outline-none"
        />
        <button
          type="submit"
          disabled={sending || !question.trim()}
          className="rounded bg-sky-500 px-4 py-2 text-sm font-medium text-slate-950 hover:bg-sky-400 disabled:cursor-not-allowed disabled:bg-slate-700 disabled:text-slate-500"
        >
          Envoyer
        </button>
      </form>
    </div>
  );
}
