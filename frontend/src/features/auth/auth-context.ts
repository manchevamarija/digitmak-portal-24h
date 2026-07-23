import { createContext } from "react";
export type PortalUser = {
  id: string;
  email: string;
  firstName: string;
  lastName: string;
  organizationId?: string;
  roles: string[];
};
export type AuthValue = {
  user: PortalUser | null;
  login: (email: string, password: string) => Promise<void>;
  logout: () => Promise<void>;
  isAuthenticated: boolean;
  isLoading: boolean;
};
export const AuthContext = createContext<AuthValue | null>(null);
