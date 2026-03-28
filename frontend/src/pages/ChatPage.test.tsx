import '@testing-library/jest-dom/vitest';
import { fireEvent, render, screen, waitFor } from '@testing-library/react';
import { beforeEach, describe, expect, it, vi } from 'vitest';
import { getChatHistory, sendChatMessage } from '../services/chatApi';
import { ChatPage } from './ChatPage';

vi.mock('../services/chatApi', () => ({
  getChatHistory: vi.fn(),
  sendChatMessage: vi.fn()
}));

const mockedGetChatHistory = vi.mocked(getChatHistory);
const mockedSendChatMessage = vi.mocked(sendChatMessage);

describe('ChatPage', () => {
  beforeEach(() => {
    vi.resetAllMocks();
    mockedGetChatHistory.mockResolvedValue({
      conversationId: undefined,
      messages: []
    });
    mockedSendChatMessage.mockResolvedValue({
      conversationId: 'conv_123',
      assistantMessage: {
        role: 'assistant',
        content: 'Try two fast dinners and one leftovers night this week.',
        timestamp: new Date().toISOString()
      }
    });
  });

  it('renders empty state when no messages exist', async () => {
    render(<ChatPage />);

    expect(await screen.findByText('Chat Assistant')).toBeInTheDocument();
    expect(screen.getByText("No messages yet. Start by asking for this week's meal plan.")).toBeInTheDocument();
  });

  it('sends a message and renders assistant response', async () => {
    render(<ChatPage />);

    await screen.findByText('Chat Assistant');

    fireEvent.change(screen.getByLabelText('Message'), {
      target: { value: 'Can you plan 3 quick dinners?' }
    });

    fireEvent.click(screen.getByRole('button', { name: 'Send' }));

    await waitFor(() => {
      expect(mockedSendChatMessage).toHaveBeenCalledWith({
        message: 'Can you plan 3 quick dinners?',
        conversationId: undefined
      });
    });

    expect(await screen.findByText('Try two fast dinners and one leftovers night this week.')).toBeInTheDocument();
  });
});
