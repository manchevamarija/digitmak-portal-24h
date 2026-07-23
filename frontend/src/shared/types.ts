export type Language = "mk" | "en" | "sq";
export type View =
  | "home"
  | "services"
  | "help"
  | "contact"
  | "training"
  | "register"
  | "forgot"
  | "reset"
  | "verify"
  | "organization"
  | "subscription-invite"
  | "privacy"
  | "terms"
  | "dashboard"
  | "staff"
  | "admin";
export type Navigate = (
  view: View,
  options?: { tab?: string; ticket?: string },
) => void;
