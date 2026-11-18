import { configureStore } from '@reduxjs/toolkit';
import authReducer from '../features/auth/authSlice';
import flagsReducer from '../features/flags/flagsSlice';
import changeRequestsReducer from '../features/changeRequests/changeRequestsSlice';
import apiKeysReducer from '../features/apiKeys/apiKeysSlice';

export const store = configureStore({
  reducer: {
    auth: authReducer,
    flags: flagsReducer,
    changeRequests: changeRequestsReducer,
    apiKeys: apiKeysReducer,
  },
});

export type RootState = ReturnType<typeof store.getState>;
export type AppDispatch = typeof store.dispatch;
