import { useEffect, useState } from "react";
import Icon from "./Icon";

type Theme = "light" | "dark";

function getInitialTheme(): Theme {
  return document.documentElement.dataset.theme === "dark" ? "dark" : "light";
}

export default function ThemeToggle() {
  const [theme, setTheme] = useState<Theme>(getInitialTheme);

  useEffect(() => {
    document.documentElement.dataset.theme = theme;
    localStorage.setItem("theme", theme);
  }, [theme]);

  const label = theme === "dark" ? "Switch to day mode" : "Switch to night mode";

  return (
    <button
      type="button"
      className="btn btn-icon"
      onClick={() => setTheme((t) => (t === "dark" ? "light" : "dark"))}
      aria-label={label}
      title={label}
    >
      {theme === "dark" ? <Icon name="sun" /> : <Icon name="moon" />}
    </button>
  );
}
