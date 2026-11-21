import { useState } from "react";
import { deleteCurrentUser, setDigestsStopped, type User } from "../../api/client";
import ConfirmDialog from "../../components/ConfirmDialog";
import { errorMessage } from "../../lib/errorMessage";
import ProfileDialog from "./ProfileDialog";
import SettingsDialog from "./SettingsDialog";
import UserMenu from "./UserMenu";

export default function AccountControls({
  user,
  onUserChange,
  onLogin,
  onLogout,
  onError,
}: {
  user: User | null;
  onUserChange: (user: User) => void;
  onLogin: () => void;
  onLogout: () => void;
  onError: (message: string | null) => void;
}) {
  const [isProfileOpen, setIsProfileOpen] = useState(false);
  const [isSettingsOpen, setIsSettingsOpen] = useState(false);
  const [isDeleteAccountConfirmOpen, setIsDeleteAccountConfirmOpen] = useState(false);
  const [isDeletingAccount, setIsDeletingAccount] = useState(false);

  async function handleToggleDigests() {
    if (!user) return;
    try {
      const stopped = !user.digestsStopped;
      await setDigestsStopped(stopped);
      onUserChange({ ...user, digestsStopped: stopped });
    } catch (err) {
      onError(errorMessage(err));
    }
  }

  async function handleDeleteAccount() {
    setIsDeletingAccount(true);
    onError(null);
    try {
      await deleteCurrentUser();
      setIsDeleteAccountConfirmOpen(false);
      setIsSettingsOpen(false);
      onLogout();
    } catch (err) {
      onError(errorMessage(err));
      setIsDeletingAccount(false);
    }
  }

  if (!user) {
    return (
      <button className="btn btn-primary btn-sm" onClick={onLogin}>
        Sign in
      </button>
    );
  }

  return (
    <>
      <UserMenu
        user={user}
        onOpenProfile={() => setIsProfileOpen(true)}
        onOpenSettings={() => setIsSettingsOpen(true)}
        onSignOut={onLogout}
      />

      {isProfileOpen && (
        <ProfileDialog user={user} onClose={() => setIsProfileOpen(false)} onSaved={onUserChange} />
      )}

      {isSettingsOpen && (
        <SettingsDialog
          digestsStopped={user.digestsStopped}
          onToggleDigests={handleToggleDigests}
          onClose={() => setIsSettingsOpen(false)}
          onRequestDeleteAccount={() => setIsDeleteAccountConfirmOpen(true)}
        />
      )}

      {isDeleteAccountConfirmOpen && (
        <ConfirmDialog
          title="Delete your account?"
          message="Are you sure you want to delete your account?"
          confirmLabel="Delete account"
          isBusy={isDeletingAccount}
          onConfirm={handleDeleteAccount}
          onCancel={() => setIsDeleteAccountConfirmOpen(false)}
        />
      )}
    </>
  );
}
