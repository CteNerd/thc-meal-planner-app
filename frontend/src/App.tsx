import { Navigate, Route, Routes } from 'react-router-dom';
import { AppShell } from './components/layout/AppShell';
import { RequireAuth } from './components/auth/RequireAuth';
import { ChatPage } from './pages/ChatPage';
import { CookbookPage } from './pages/CookbookPage';
import { DashboardPage } from './pages/DashboardPage';
import { GroceryListPage } from './pages/GroceryListPage';
import { LoginPage } from './pages/LoginPage';
import { MealPlansPage } from './pages/MealPlansPage';
import { ProfilePage } from './pages/ProfilePage';

function App() {
  return (
    <Routes>
      <Route path="/login" element={<LoginPage />} />
      <Route
        path="/dashboard"
        element={
          <RequireAuth>
            <AppShell>
              <DashboardPage />
            </AppShell>
          </RequireAuth>
        }
      />
      <Route
        path="/meal-plans"
        element={
          <RequireAuth>
            <AppShell>
              <MealPlansPage />
            </AppShell>
          </RequireAuth>
        }
      />
      <Route
        path="/cookbook"
        element={
          <RequireAuth>
            <AppShell>
              <CookbookPage />
            </AppShell>
          </RequireAuth>
        }
      />
      <Route
        path="/grocery-list"
        element={
          <RequireAuth>
            <AppShell>
              <GroceryListPage />
            </AppShell>
          </RequireAuth>
        }
      />
      <Route
        path="/chat"
        element={
          <RequireAuth>
            <AppShell>
              <ChatPage />
            </AppShell>
          </RequireAuth>
        }
      />
      <Route
        path="/profile"
        element={
          <RequireAuth>
            <AppShell>
              <ProfilePage />
            </AppShell>
          </RequireAuth>
        }
      />
      <Route path="*" element={<Navigate to="/dashboard" replace />} />
    </Routes>
  );
}

export default App;
