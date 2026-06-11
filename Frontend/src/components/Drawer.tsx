import React from "react";
import { X } from "lucide-react";

interface DrawerProps {
  isOpen: boolean;
  title: string;
  onClose: () => void;
  children: React.ReactNode;
}

export const Drawer: React.FC<DrawerProps> = ({ isOpen, title, onClose, children }) => {
  if (!isOpen) return null;

  return (
    <div className="drawer-overlay" onClick={onClose}>
      <div className="drawer-content" onClick={(e) => e.stopPropagation()}>
        <header className="drawer-header">
          <h2>{title}</h2>
          <button className="drawer-close" onClick={onClose} aria-label="Close panel" type="button">
            <X size={20} />
          </button>
        </header>
        <div className="drawer-body">
          {children}
        </div>
      </div>
    </div>
  );
};
