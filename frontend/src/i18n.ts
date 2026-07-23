import i18n from "i18next";
import { initReactI18next } from "react-i18next";

const resources = {
  mk: {
    translation: {
      home: "Почетна",
      services: "Услуги",
      training: "Обуки",
      help: "AI поддршка",
      contact: "Контакт",
      portal: "Мој портал",
      privacy: "Приватност",
      terms: "Услови",
      tagline: "Европски центар за дигитални иновации за Северна Македонија.",
      notifications: "Известувања",
      noNotifications: "Нема известувања.",
    },
  },
  en: {
    translation: {
      home: "Home",
      services: "Services",
      training: "Training",
      help: "AI Help Desk",
      contact: "Contact",
      portal: "My portal",
      privacy: "Privacy",
      terms: "Terms",
      tagline: "European Digital Innovation Hub for North Macedonia.",
      notifications: "Notifications",
      noNotifications: "No notifications.",
    },
  },
  sq: {
    translation: {
      home: "Ballina",
      services: "Shërbimet",
      training: "Trajnime",
      help: "AI Mbështetje",
      contact: "Kontakt",
      portal: "Portali im",
      privacy: "Privatësia",
      terms: "Kushtet",
      tagline:
        "Qendra Evropiane për Inovacion Digjital për Maqedoninë e Veriut.",
      notifications: "Njoftimet",
      noNotifications: "Nuk ka njoftime.",
    },
  },
};

void i18n.use(initReactI18next).init({
  resources,
  lng:
    typeof window !== "undefined" &&
    typeof window.localStorage?.getItem === "function"
      ? (window.localStorage.getItem("digitmak.language") ?? "mk")
      : "mk",
  fallbackLng: "mk",
  interpolation: { escapeValue: false },
});
export default i18n;
