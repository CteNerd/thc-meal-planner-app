import { FormEvent, useEffect, useState } from 'react';
import { Button } from '../components/ui/Button';
import { Card } from '../components/ui/Card';
import { getApiErrorMessage } from '../services/api';
import { getChatHistory, sendChatMessage } from '../services/chatApi';
import type { ChatMessage } from '../types';

export function ChatPage() {
  const [messages, setMessages] = useState<ChatMessage[]>([]);
  const [conversationId, setConversationId] = useState<string | undefined>(undefined);
  const [draft, setDraft] = useState('');
  const [isLoading, setIsLoading] = useState(true);
  const [isBusy, setIsBusy] = useState(false);
  const [error, setError] = useState<string | null>(null);

  useEffect(() => {
    let active = true;

    async function loadHistory() {
      try {
        setIsLoading(true);
        setError(null);
        const history = await getChatHistory(undefined, 30);

        if (!active) {
          return;
        }

        setConversationId(history.conversationId ?? undefined);
        setMessages(history.messages);
      } catch (err) {
        if (!active) {
          return;
        }

        setError(getApiErrorMessage(err, 'Unable to load conversation history.'));
      } finally {
        if (active) {
          setIsLoading(false);
        }
      }
    }

    void loadHistory();

    return () => {
      active = false;
    };
  }, []);

  async function sendMessage(message: string) {
    const trimmedMessage = message.trim();
    if (!trimmedMessage || isBusy) {
      return;
    }

    const optimisticUserMessage: ChatMessage = {
      role: 'user',
      content: trimmedMessage,
      timestamp: new Date().toISOString()
    };

    setError(null);
    setDraft('');
    setMessages((current) => [...current, optimisticUserMessage]);
    setIsBusy(true);

    try {
      const response = await sendChatMessage({
        message: trimmedMessage,
        conversationId
      });

      setConversationId(response.conversationId);
      setMessages((current) => [...current, response.assistantMessage]);
    } catch (err) {
      setError(getApiErrorMessage(err, 'Unable to send message.'));
    } finally {
      setIsBusy(false);
    }
  }

  async function handleSubmit(event: FormEvent<HTMLFormElement>) {
    event.preventDefault();
    await sendMessage(draft);
  }

  return (
    <Card>
      <div className="flex items-start justify-between gap-3">
        <div>
          <h2 className="text-2xl font-semibold text-slate-900">Chat Assistant</h2>
          <p className="mt-2 text-sm text-slate-600">
            Ask for meal planning help, recipe ideas, grocery list changes, and pantry guidance.
          </p>
        </div>
      </div>

      {error ? (
        <p role="alert" className="mt-4 rounded-md border border-rose-200 bg-rose-50 px-3 py-2 text-sm text-rose-700">
          {error}
        </p>
      ) : null}

      <div className="mt-5 space-y-3 rounded-xl border border-slate-200 bg-slate-50 p-4">
        {isLoading ? <p className="text-sm text-slate-500">Loading conversation...</p> : null}

        {!isLoading && messages.length === 0 ? (
          <p className="text-sm text-slate-500">No messages yet. Start by asking for this week&apos;s meal plan.</p>
        ) : null}

        {messages.map((message, index) => (
          <div
            key={`${message.timestamp}-${index}`}
            className={[
              'max-w-[85%] rounded-xl px-3 py-2 text-sm whitespace-pre-wrap',
              message.role === 'user'
                ? 'ml-auto bg-slate-900 text-white'
                : 'mr-auto border border-slate-200 bg-white text-slate-800'
            ].join(' ')}
          >
            {message.role === 'assistant' ? <MarkdownMessage text={message.content} /> : message.content}
            {message.role === 'assistant' && message.requiresConfirmation ? (
              <div className="mt-3 flex flex-wrap gap-2">
                <Button
                  type="button"
                  onClick={() => void sendMessage('Confirm')}
                  disabled={isBusy}
                  className="!px-3 !py-1.5 !text-xs"
                >
                  Confirm
                </Button>
                <Button
                  type="button"
                  onClick={() => void sendMessage('Cancel')}
                  disabled={isBusy}
                  variant="ghost"
                  className="!px-3 !py-1.5 !text-xs"
                >
                  Cancel
                </Button>
              </div>
            ) : null}
          </div>
        ))}

        {isBusy ? <p className="text-sm text-slate-500">Assistant is typing...</p> : null}
      </div>

      <form onSubmit={handleSubmit} className="mt-4 flex flex-col gap-3 sm:flex-row">
        <label htmlFor="chat-input" className="sr-only">
          Message
        </label>
        <input
          id="chat-input"
          value={draft}
          onChange={(event) => setDraft(event.target.value)}
          placeholder="Ask about meal plans, recipes, groceries, or pantry staples"
          className="w-full rounded-md border border-slate-300 px-3 py-2 text-sm text-slate-900 outline-none transition focus:border-slate-600"
          disabled={isBusy}
        />
        <Button type="submit" disabled={isBusy || draft.trim().length === 0}>
          Send
        </Button>
      </form>
    </Card>
  );
}

function MarkdownMessage({ text }: { text: string }) {
  const html = toSafeHtml(text);

  return <span dangerouslySetInnerHTML={{ __html: html }} />;
}

function toSafeHtml(markdown: string): string {
  const escaped = escapeHtml(markdown);

  return escaped
    .replace(/\*\*(.+?)\*\*/g, '<strong>$1</strong>')
    .replace(/`([^`]+)`/g, '<code class="rounded bg-slate-100 px-1 py-0.5 text-xs text-slate-800">$1</code>')
    .replace(/\n/g, '<br/>');
}

function escapeHtml(value: string): string {
  return value
    .replace(/&/g, '&amp;')
    .replace(/</g, '&lt;')
    .replace(/>/g, '&gt;')
    .replace(/"/g, '&quot;')
    .replace(/'/g, '&#39;');
}