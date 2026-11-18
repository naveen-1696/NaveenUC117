import { createAsyncThunk, createSlice } from '@reduxjs/toolkit';
import * as api from '../../api/apiClient';

interface AuthState {
  token: string | null;
  userId: string | null;
  email: string | null;
  role: string | null;
  loading: boolean;
  error: string | null;
}

const stored = (() => {
  try { return JSON.parse(localStorage.getItem('user') || 'null'); } catch { return null; }
})();

const initialState: AuthState = {
  token: localStorage.getItem('token'),
  userId: stored?.userId ?? null,
  email: stored?.email ?? null,
  role: stored?.role ?? null,
  loading: false,
  error: null,
};

export const loginThunk = createAsyncThunk(
  'auth/login',
  async (creds: { email: string; password: string }, { rejectWithValue }) => {
    try {
      const { data } = await api.login(creds.email, creds.password);
      localStorage.setItem('token', data.token);
      localStorage.setItem('user', JSON.stringify(data));
      return data;
    } catch (err: any) {
      return rejectWithValue(err.response?.data?.error || 'Login failed');
    }
  }
);

const authSlice = createSlice({
  name: 'auth',
  initialState,
  reducers: {
    logout(state) {
      state.token = null;
      state.userId = null;
      state.email = null;
      state.role = null;
      localStorage.removeItem('token');
      localStorage.removeItem('user');
    },
  },
  extraReducers: (builder) => {
    builder
      .addCase(loginThunk.pending, (s) => { s.loading = true; s.error = null; })
      .addCase(loginThunk.fulfilled, (s, { payload }) => {
        s.loading = false;
        s.token = payload.token;
        s.userId = payload.userId;
        s.email = payload.email;
        s.role = payload.role;
      })
      .addCase(loginThunk.rejected, (s, { payload }) => {
        s.loading = false;
        s.error = payload as string;
      });
  },
});

export const { logout } = authSlice.actions;
export default authSlice.reducer;
