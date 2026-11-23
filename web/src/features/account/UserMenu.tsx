import { useEffect, useRef, useState } from "react";
import type { User } from "../../api/client";
import Icon from "../../components/Icon";

export default function UserMenu({
  user,
  onOpenProfile,
  onOpenSettings,
  onSignOut,
}: {
  user: User;
  onOpenProfile: () => void;
  onOpenSettings: () => void;
  onSignOut: () => void;
}) {
  const [isOpen, setIsOpen] = useState(false);
  const menuRef = useRef<HTMLDivElement>(null);

  useEffect(() => {
    function onPointerDown(e: MouseEvent) {
      if (menuRef.current && !menuRef.current.contains(e.target as Node)) {
        setIsOpen(false);
      }
    }
    function onKeyDown(e: KeyboardEvent) {
      if (e.key === "Escape") {
        setIsOpen(false);
      }
    }
    document.addEventListener("mousedown", onPointerDown);
    window.addEventListener("keydown", onKeyDown);
    return () => {
      document.removeEventListener("mousedown", onPointerDown);
      window.removeEventListener("keydown", onKeyDown);
    };
  }, []);

  const initial = user.username.trim()[0]?.toUpperCase() ?? "?";

  return (
    <div className="user-menu" ref={menuRef}>
      <button
        className="user-menu-trigger"
        onClick={() => setIsOpen((open) => !open)}
        aria-haspopup="menu"
        aria-expanded={isOpen}
      >
        <span className="avatar" aria-hidden="true">
          {initial}
        </span>
        <span className="user-chip-text">
          <span className="user-chip-name">{user.username}</span>
        </span>
        <Icon name="chevronDown" size={14} />
      </button>

      {isOpen && (
        <div className="user-menu-dropdown" role="menu">
          <button
            role="menuitem"
            className="user-menu-item"
            onClick={() => {
              setIsOpen(false);
              onOpenProfile();
            }}
          >
            <Icon name="profile" />
            Profile
          </button>
          <button
            role="menuitem"
            className="user-menu-item"
            onClick={() => {
              setIsOpen(false);
              onOpenSettings();
            }}
          >
            <Icon name="settings" />
            Settings
          </button>
          <div className="user-menu-divider" />
          <button
            role="menuitem"
            className="user-menu-item"
            onClick={() => {
              setIsOpen(false);
              onSignOut();
            }}
          >
            <Icon name="signOut" />
            Sign out
          </button>
        </div>
      )}
    </div>
  );
}
