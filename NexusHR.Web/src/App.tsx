import { lazy, Suspense } from "react";
import { Alert } from "@mui/material";
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
  OnboardingReaderRoles,
  hasAnyRole,
  NexusHrRoles,
} from "./auth/roles";
const CandidateDetailPage = lazy(() => import("./features/candidates/CandidateDetailPage"));
const CandidateListPage = lazy(() => import("./features/candidates/CandidateListPage"));
const OnboardingPage = lazy(() => import("./features/onboarding/OnboardingPage"));
const CreateCandidateFromCvPage = lazy(() => import("./features/candidates/CreateCandidateFromCvPage"));
const HiringProcessDetailPage = lazy(() => import("./features/hiring/HiringProcessDetailPage"));
const PublicOfferResponsePage = lazy(() => import("./features/publicOffers/PublicOfferResponsePage"));

function App() {
  const defaultPath =
    hasAnyRole(CandidateReaderRoles)
      ? "/candidates"
      : hasAnyRole(OnboardingReaderRoles) ? "/onboardings" : "/access-denied";

  return (
    <BrowserRouter>
      <AuthBar />

      <Suspense fallback={<Alert severity="info">Sayfa yükleniyor…</Alert>}>
      <Routes>
        <Route path="/onboardings" element={<RoleGuard allowedRoles={OnboardingReaderRoles}><OnboardingPage key="list" /></RoleGuard>} />
        <Route path="/onboardings/:id" element={<RoleGuard allowedRoles={OnboardingReaderRoles}><OnboardingPage key="detail" /></RoleGuard>} />
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
  path="/hiring-processes/:id"
  element={
    <RoleGuard
      allowedRoles={
        CandidateReaderRoles
      }
    >
      <HiringProcessDetailPage />
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

        <Route
  path="/public/offers/respond"
  element={<PublicOfferResponsePage />}
/>
      </Routes>
      </Suspense>
    </BrowserRouter>
  );
}

export default App;
