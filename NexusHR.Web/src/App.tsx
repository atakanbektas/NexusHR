import {
  BrowserRouter,
  Navigate,
  Route,
  Routes,
} from "react-router-dom";
import AuthBar from "./auth/AuthBar";
import CandidateDetailPage from
  "./features/candidates/CandidateDetailPage";
import CandidateListPage from
  "./features/candidates/CandidateListPage";
import CreateCandidateFromCvPage from
  "./features/candidates/CreateCandidateFromCvPage";

function App() {
  return (
    <BrowserRouter>
      <AuthBar />

      <Routes>
        <Route
          path="/"
          element={
            <Navigate
              to="/candidates"
              replace
            />
          }
        />

        <Route
          path="/candidates"
          element={<CandidateListPage />}
        />

        <Route
          path="/candidates/new"
          element={<CreateCandidateFromCvPage />}
        />

        <Route
          path="/candidates/:id"
          element={<CandidateDetailPage />}
        />

        <Route
          path="*"
          element={
            <Navigate
              to="/candidates"
              replace
            />
          }
        />
      </Routes>
    </BrowserRouter>
  );
}

export default App;