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
import CandidateDetailPage from
  "./features/candidates/CandidateDetailPage";
import CandidateListPage from
  "./features/candidates/CandidateListPage";
import OnboardingPage from "./features/onboarding/OnboardingPage";
import CreateCandidateFromCvPage from
  "./features/candidates/CreateCandidateFromCvPage";
  import HiringProcessDetailPage from
  "./features/hiring/HiringProcessDetailPage";
  import PublicOfferResponsePage from
  "./features/publicOffers/PublicOfferResponsePage";

function App() {
  const defaultPath =
    hasAnyRole(CandidateReaderRoles)
      ? "/candidates"
      : hasAnyRole(OnboardingReaderRoles) ? "/onboardings" : "/access-denied";

  return (
    <BrowserRouter>
      <AuthBar />

      <Routes>
        <Route path="/onboardings" element={<RoleGuard allowedRoles={OnboardingReaderRoles}><OnboardingPage key="list" /></RoleGuard>} />
        <Route path="/onboardings/:id" element={<RoleGuard allowedRoles={OnboardingReaderRoles}><OnboardingPage key={window.location.pathname} /></RoleGuard>} />
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
    </BrowserRouter>
  );
}

export default App;
