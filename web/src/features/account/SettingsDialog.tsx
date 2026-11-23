import Modal from "../../components/Modal";

export default function SettingsDialog({
  digestsStopped,
  onToggleDigests,
  onClose,
  onRequestDeleteAccount,
}: {
  digestsStopped: boolean;
  onToggleDigests: () => void;
  onClose: () => void;
  onRequestDeleteAccount: () => void;
}) {
  return (
    <Modal titleId="settings-title" onClose={onClose}>
      <h3 id="settings-title">Settings</h3>
      <p className="modal-subtitle">Manage your account.</p>

      <p className="modal-message">
        {digestsStopped
          ? "Digest emails are off. Your searches are kept."
          : "Digest emails are on."}
      </p>
      <button className="btn btn-ghost" onClick={onToggleDigests}>
        {digestsStopped ? "Turn Digests back on" : "Stop all Digests"}
      </button>

      <div className="danger-zone">
        <p className="danger-zone-title">Delete account</p>
        <p className="modal-message">
          You have 30 days to recover your account by signing in again. After that, your account and all subscriptions will be permanently deleted.
        </p>
        <button className="btn btn-danger-outline" onClick={onRequestDeleteAccount}>
          Delete account
        </button>
      </div>

      <div className="modal-actions">
        <button className="btn btn-ghost" onClick={onClose} autoFocus>
          Close
        </button>
      </div>
    </Modal>
  );
}
