import {
  BrowserRouter,
  Navigate,
  Route,
  Routes,
} from "react-router-dom";
import AccessDeniedPage from
  "./auth/AccessDeniedPage";
import AuthBar from "./auth/AuthBar";
import RoleGuard from "./auth/RoleGuard";
import {
  CandidateReaderRoles,
  hasAnyRole,
  NexusHrRoles,
} from "./auth/roles";
import CandidateDetailPage from
  "./features/candidates/CandidateDetailPage";
import CandidateListPage from
  "./features/candidates/CandidateListPage";
import CreateCandidateFromCvPage from
  "./features/candidates/CreateCandidateFromCvPage";

function App() {
  const defaultPath =
    hasAnyRole(CandidateReaderRoles)
      ? "/candidates"
      : "/access-denied";

  return (
    <BrowserRouter>
      <AuthBar />

      <Routes>
        <Route
          path="/"
          element={
            <Navigate
              to={defaultPath}
              replace
            />
          }
        />

        <Route
          path="/access-denied"
          element={<AccessDeniedPage />}
        />

        <Route
          path="/candidates"
          element={
            <RoleGuard
              allowedRoles={
                CandidateReaderRoles
              }
            >
              <CandidateListPage />
            </RoleGuard>
          }
        />

        <Route
          path="/candidates/new"
          element={
            <RoleGuard
              allowedRoles={[
                NexusHrRoles.HrSpecialist,
              ]}
            >
              <CreateCandidateFromCvPage />
            </RoleGuard>
          }
        />

        <Route
          path="/candidates/:id"
          element={
            <RoleGuard
              allowedRoles={
                CandidateReaderRoles
              }
            >
              <CandidateDetailPage />
            </RoleGuard>
          }
        />

        <Route
          path="*"
          element={
            <Navigate
              to={defaultPath}
              replace
            />
          }
        />
      </Routes>
    </BrowserRouter>
  );
}

export default App;