import i18n from "i18next";
import { initReactI18next } from "react-i18next";
import en from "./locales/en.json";
import ar from "./locales/ar.json";

// FR-005: "Arabic and English interface with correct RTL/LTR layouts... switching language
// retains current page/context and persists preference." Persistence is handled in
// shell/LanguageSwitch.tsx (localStorage); layout direction is driven off i18n.language in App.tsx.
void i18n.use(initReactI18next).init({
  resources: {
    en: { translation: en },
    ar: { translation: ar },
  },
  lng: localStorage.getItem("bankops-language") ?? "en",
  fallbackLng: "en",
  interpolation: { escapeValue: false },
});

export default i18n;
