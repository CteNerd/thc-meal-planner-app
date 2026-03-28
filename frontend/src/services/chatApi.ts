import type { ChatHistoryResponse, ChatMessageRequestPayload, ChatMessageResponse } from '../types';
import { apiGet, apiPost } from './api';

export async function sendChatMessage(payload: ChatMessageRequestPayload): Promise<ChatMessageResponse> {
  return await apiPost<ChatMessageResponse, ChatMessageRequestPayload>('/chat/message', payload);
}

export async function getChatHistory(conversationId?: string, limit = 50): Promise<ChatHistoryResponse> {
  const params = new URLSearchParams();

  if (conversationId) {
    params.set('conversationId', conversationId);
  }

  params.set('limit', String(limit));

  const query = params.toString();
  return await apiGet<ChatHistoryResponse>(`/chat/history${query ? `?${query}` : ''}`);
}
