import { useTranslation } from "react-i18next";
import type { Language } from "./types";

export function usePortalLanguage(): Language {
  const { i18n } = useTranslation();
  const language = i18n.resolvedLanguage?.split("-")[0];
  return language === "en" || language === "sq" ? language : "mk";
}
