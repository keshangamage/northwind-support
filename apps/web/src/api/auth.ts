import type { AuthSession, AuthUser } from '../types/api';
import { request } from './client';

export const authApi = {
  login: (email: string, password: string) => request<AuthSession>('/auth/login', {
    method: 'POST',
    body: JSON.stringify({ email, password }),
  }),

  me: () => request<AuthUser>('/auth/me'),
};
