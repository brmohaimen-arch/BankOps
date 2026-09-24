import { Button } from "antd";
import { useTranslation } from "react-i18next";

export function LanguageSwitch() {
  const { i18n } = useTranslation();

  const toggle = () => {
    const next = i18n.language === "ar" ? "en" : "ar";
    void i18n.changeLanguage(next);
    localStorage.setItem("bankops-language", next);
    document.documentElement.dir = next === "ar" ? "rtl" : "ltr";
    document.documentElement.lang = next;
  };

  return <Button onClick={toggle}>{i18n.language === "ar" ? "English" : "العربية"}</Button>;
}
