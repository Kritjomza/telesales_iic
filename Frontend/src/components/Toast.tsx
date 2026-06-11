import React, { useEffect } from "react";
import { AlertCircle, CheckCircle, Info, X } from "lucide-react";

export interface ToastItem {
  id: string;
  message: string;
  type: "success" | "error" | "info";
}

interface ToastProps {
  toasts: ToastItem[];
  onClose: (id: string) => void;
}

export const Toast: React.FC<ToastProps> = ({ toasts, onClose }) => {
  return (
    <div className="toast-container">
      {toasts.map((t) => (
        <ToastMessage key={t.id} toast={t} onClose={onClose} />
      ))}
    </div>
  );
};

const ToastMessage: React.FC<{ toast: ToastItem; onClose: (id: string) => void }> = ({ toast, onClose }) => {
  useEffect(() => {
    const timer = setTimeout(() => {
      onClose(toast.id);
    }, 4000);
    return () => clearTimeout(timer);
  }, [toast.id, onClose]);

  const icons = {
    success: <CheckCircle className="toast-icon text-success" size={18} />,
    error: <AlertCircle className="toast-icon text-danger" size={18} />,
    info: <Info className="toast-icon text-info" size={18} />
  };

  return (
    <div className={`toast-box ${toast.type}`}>
      {icons[toast.type]}
      <div className="toast-body">{toast.message}</div>
      <button className="toast-close" onClick={() => onClose(toast.id)} aria-label="Close notification" type="button">
        <X size={14} />
      </button>
    </div>
  );
};
