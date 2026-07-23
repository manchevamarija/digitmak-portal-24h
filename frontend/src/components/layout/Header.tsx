import { useState } from "react";
import { useTranslation } from "react-i18next";
import type { Language, Navigate, View } from "../../shared/types";
import { useAuth } from "../../features/auth/useAuth";

type Props = {
  language: Language;
  view: View;
  onLanguage: (language: Language) => void;
  onNavigate: Navigate;
};
export function Header({ language, view, onLanguage, onNavigate }: Props) {
  const [menu, setMenu] = useState(false);
  const { t, i18n } = useTranslation();
  const { user } = useAuth();
  const isAdmin = user?.roles.includes("Admin") ?? false;
  const nav: { view: View; label: string }[] = [
    { view: "home", label: t("home") },
    { view: "services", label: t("services") },
    { view: "training", label: t("training") },
    { view: "help", label: t("help") },
    ...(!isAdmin ? [{ view: "contact" as View, label: t("contact") }] : []),
  ];
  const changeLanguage = (value: Language) => {
    if (typeof window.localStorage?.setItem === "function")
      window.localStorage.setItem("digitmak.language", value);
    void i18n.changeLanguage(value);
    onLanguage(value);
  };
  return (
    <header className="site-header">
      <button
        className="brand"
        onClick={() => onNavigate("home")}
        aria-label="DigitMak home"
      >
        <img src="/brand/digitmak-logo.svg" alt="DigitMak" />
      </button>
      <button
        className="hamb"
        onClick={() => setMenu(!menu)}
        aria-label="Menu"
        aria-expanded={menu}
      >
        ☰
      </button>
      <nav className={menu ? "open" : ""}>
        {nav.map((item) => (
          <button
            key={item.view}
            className={view === item.view ? "active" : ""}
            onClick={() => {
              onNavigate(item.view);
              setMenu(false);
            }}
          >
            {item.label}
          </button>
        ))}
      </nav>
      <div className="tools">
        <select
          value={language}
          onChange={(event) => changeLanguage(event.target.value as Language)}
          aria-label="Language"
        >
          <option value="mk">МК</option>
          <option value="en">EN</option>
          <option value="sq">SQ</option>
        </select>
        <button className="portal" onClick={() => onNavigate("dashboard")}>
          {t("portal")}
        </button>
      </div>
    </header>
  );
}
