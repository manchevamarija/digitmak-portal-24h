import { useTranslation } from "react-i18next";
import type { Navigate } from "../../shared/types";
export function Footer({ onNavigate }: { onNavigate: Navigate }) {
  const { t } = useTranslation();
  return (
    <footer>
      <div className="brand invert">
        <img src="/brand/digitmak-logo.svg" alt="DigitMak" />
      </div>
      <p>{t("tagline")}</p>
      <div>
        <button onClick={() => onNavigate("services")}>{t("services")}</button>
        <button onClick={() => onNavigate("contact")}>{t("contact")}</button>
        <button onClick={() => onNavigate("privacy")}>{t("privacy")}</button>
        <button onClick={() => onNavigate("terms")}>{t("terms")}</button>
      </div>
      <small>© 2026 DigitMak</small>
    </footer>
  );
}
