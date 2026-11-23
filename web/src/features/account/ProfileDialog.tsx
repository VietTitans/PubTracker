import { useState } from "react";
import { updateCurrentUser, type User } from "../../api/client";
import Modal from "../../components/Modal";
import Spinner from "../../components/Spinner";
import { errorMessage } from "../../lib/errorMessage";

export default function ProfileDialog({
  user,
  onClose,
  onSaved,
}: {
  user: User;
  onClose: () => void;
  onSaved: (user: User) => void;
}) {
  const [username, setUsername] = useState(user.username);
  const [email, setEmail] = useState(user.email);
  const [isSaving, setIsSaving] = useState(false);
  const [error, setError] = useState<string | null>(null);

  async function handleSubmit(e: React.FormEvent) {
    e.preventDefault();
    setIsSaving(true);
    setError(null);
    try {
      const updated = await updateCurrentUser({ username, email });
      onSaved(updated);
      onClose();
    } catch (err) {
      setError(errorMessage(err));
    } finally {
      setIsSaving(false);
    }
  }

  return (
    <Modal titleId="profile-title" onClose={onClose}>
      <h3 id="profile-title">Profile</h3>

      {error && (
        <p className="alert" role="alert">
          {error}
        </p>
      )}

      <form onSubmit={handleSubmit}>
        <div className="form-field">
          <label className="field-label" htmlFor="profile-username">
            Username
          </label>
          <input
            id="profile-username"
            className="text-input"
            value={username}
            onChange={(e) => setUsername(e.target.value)}
            required
          />
        </div>

        <div className="form-field">
          <label className="field-label" htmlFor="profile-email">
            Email
          </label>
          <input
            id="profile-email"
            type="email"
            className="text-input"
            value={email}
            onChange={(e) => setEmail(e.target.value)}
            required
          />
        </div>

        <div className="modal-actions">
          <button type="button" className="btn btn-ghost" onClick={onClose} disabled={isSaving}>
            Cancel
          </button>
          <button type="submit" className="btn btn-primary" disabled={isSaving}>
            {isSaving ? <Spinner /> : "Save"}
          </button>
        </div>
      </form>
    </Modal>
  );
}
