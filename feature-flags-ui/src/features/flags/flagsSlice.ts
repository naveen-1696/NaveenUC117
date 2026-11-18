import { createAsyncThunk, createSlice } from '@reduxjs/toolkit';
import * as api from '../../api/apiClient';

interface FlagsState {
  items: api.Flag[];
  totalCount: number;
  page: number;
  pageSize: number;
  search: string;
  loading: boolean;
  error: string | null;
  environments: api.Environment[];
}

const initialState: FlagsState = {
  items: [],
  totalCount: 0,
  page: 1,
  pageSize: 20,
  search: '',
  loading: false,
  error: null,
  environments: [],
};

export const fetchFlags = createAsyncThunk(
  'flags/fetchAll',
  async ({ page, pageSize, search }: { page: number; pageSize: number; search?: string }) => {
    const { data } = await api.getFlags(page, pageSize, search);
    return data;
  }
);

export const fetchEnvironments = createAsyncThunk('flags/fetchEnvironments', async () => {
  const { data } = await api.getEnvironments();
  return data;
});

export const createFlag = createAsyncThunk(
  'flags/create',
  async (payload: { key: string; name: string; description?: string; type: string }, { rejectWithValue }) => {
    try {
      const { data } = await api.createFlag(payload);
      return data;
    } catch (err: any) {
      return rejectWithValue(err.response?.data?.error || 'Create failed');
    }
  }
);

export const archiveFlag = createAsyncThunk(
  'flags/archive',
  async (id: number, { rejectWithValue }) => {
    try {
      await api.archiveFlag(id);
      return id;
    } catch (err: any) {
      return rejectWithValue(err.response?.data?.error || 'Archive failed');
    }
  }
);

const flagsSlice = createSlice({
  name: 'flags',
  initialState,
  reducers: {
    setSearch(state, action) { state.search = action.payload; state.page = 1; },
    setPage(state, action) { state.page = action.payload; },
  },
  extraReducers: (builder) => {
    builder
      .addCase(fetchFlags.pending, (s) => { s.loading = true; s.error = null; })
      .addCase(fetchFlags.fulfilled, (s, { payload }) => {
        s.loading = false;
        s.items = payload.items;
        s.totalCount = payload.totalCount;
        s.page = payload.page;
        s.pageSize = payload.pageSize;
      })
      .addCase(fetchFlags.rejected, (s, { error }) => { s.loading = false; s.error = error.message ?? 'Error'; })
      .addCase(fetchEnvironments.fulfilled, (s, { payload }) => { s.environments = payload; })
      .addCase(createFlag.fulfilled, (s, { payload }) => { s.items.unshift(payload); s.totalCount += 1; })
      .addCase(archiveFlag.fulfilled, (s, { payload }) => {
        const f = s.items.find((x) => x.id === payload);
        if (f) f.isArchived = true;
      });
  },
});

export const { setSearch, setPage } = flagsSlice.actions;
export default flagsSlice.reducer;
