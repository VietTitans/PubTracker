import Modal from "./Modal";
import Spinner from "./Spinner";

export default function ConfirmDialog({
  title,
  message,
  confirmLabel,
  isBusy,
  onConfirm,
  onCancel,
}: {
  title: string;
  message: string;
  confirmLabel: string;
  isBusy: boolean;
  onConfirm: () => void;
  onCancel: () => void;
}) {
  return (
    <Modal titleId="confirm-dialog-title" role="alertdialog" onClose={onCancel}>
      <h3 id="confirm-dialog-title">{title}</h3>
      <p className="modal-message">{message}</p>
      <div className="modal-actions">
        <button className="btn btn-ghost" onClick={onCancel} disabled={isBusy}>
          Cancel
        </button>
        <button className="btn btn-danger" onClick={onConfirm} disabled={isBusy} autoFocus>
          {isBusy ? <Spinner /> : confirmLabel}
        </button>
      </div>
    </Modal>
  );
}
