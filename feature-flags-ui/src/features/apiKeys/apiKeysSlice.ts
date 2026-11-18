import { createAsyncThunk, createSlice } from '@reduxjs/toolkit';
import * as api from '../../api/apiClient';

interface ApiKeysState {
  items: api.ApiKey[];
  loading: boolean;
  error: string | null;
}

const initialState: ApiKeysState = { items: [], loading: false, error: null };

export const fetchApiKeys = createAsyncThunk('apiKeys/fetch', async () => {
  const { data } = await api.getApiKeys();
  return data;
});

export const revokeApiKey = createAsyncThunk('apiKeys/revoke', async (id: number) => {
  await api.revokeApiKey(id);
  return id;
});

const slice = createSlice({
  name: 'apiKeys',
  initialState,
  reducers: {},
  extraReducers: (builder) => {
    builder
      .addCase(fetchApiKeys.pending, (s) => { s.loading = true; })
      .addCase(fetchApiKeys.fulfilled, (s, { payload }) => { s.loading = false; s.items = payload; })
      .addCase(fetchApiKeys.rejected, (s) => { s.loading = false; })
      .addCase(revokeApiKey.fulfilled, (s, { payload }) => {
        const k = s.items.find((x) => x.id === payload);
        if (k) k.isActive = false;
      });
  },
});

export default slice.reducer;
