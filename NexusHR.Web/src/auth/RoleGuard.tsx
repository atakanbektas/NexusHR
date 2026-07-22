import type {
  ReactNode,
} from "react";
import {
  Navigate,
} from "react-router-dom";
import {
  hasAnyRole,
} from "./roles";

interface RoleGuardProps {
  allowedRoles: readonly string[];
  children: ReactNode;
}

export default function RoleGuard({
  allowedRoles,
  children,
}: RoleGuardProps) {
  if (!hasAnyRole(allowedRoles)) {
    return (
      <Navigate
        to="/access-denied"
        replace
      />
    );
  }

  return children;
}