import React, { useState } from "react";
import { Search, UserCheck } from "lucide-react";
import { Modal } from "./Modal";
import { type User } from "../domain/types";
import { normalizeRole } from "../domain/permissions";

interface AssigneeModalProps {
  isOpen: boolean;
  role: "Sale" | "Tele sale";
  onClose: () => void;
  onAssign: (userId: number) => void;
  customerName: string;
  users: User[];
}

export const AssigneeModal: React.FC<AssigneeModalProps> = ({ isOpen, role, onClose, onAssign, customerName, users }) => {
  const [searchQuery, setSearchQuery] = useState("");

  const targetRole = normalizeRole(role);
  const staffList = users.filter((u) => normalizeRole(u.roles) === targetRole);

  const filteredStaff = staffList.filter((u) =>
    u.name.toLowerCase().includes(searchQuery.toLowerCase()) ||
    u.username.toLowerCase().includes(searchQuery.toLowerCase())
  );

  return (
    <Modal isOpen={isOpen} title={`Assign ${role} to ${customerName}`} onClose={onClose}>
      <div className="assign-modal-body">
        <div className="search-field">
          <Search size={16} />
          <input
            type="text"
            placeholder={`Search ${role.toLowerCase()} by name or username...`}
            value={searchQuery}
            onChange={(e) => setSearchQuery(e.target.value)}
            aria-label="Search employee"
          />
        </div>

        <div className="table-wrap table-scroll-sm" style={{ marginTop: "12px" }}>
          <table className="modal-table" aria-label="Staff selection table">
            <thead>
              <tr>
                <th style={{ width: "15%" }}>No.</th>
                <th style={{ width: "25%" }}>Username</th>
                <th style={{ width: "45%" }}>Name</th>
                <th style={{ width: "15%" }}>&nbsp;</th>
              </tr>
            </thead>
            <tbody>
              {filteredStaff.length > 0 ? (
                filteredStaff.map((u, index) => (
                  <tr key={u.id}>
                    <td>{index + 1}</td>
                    <td>
                      <code>{u.username}</code>
                    </td>
                    <td>
                      <strong>{u.name}</strong>
                      <span className="subtext">{u.email}</span>
                    </td>
                    <td style={{ textAlign: "right" }}>
                      <button
                        className="primary-button btn-xs"
                        onClick={() => {
                        onAssign(u.id);
                        onClose();
                      }}
                      type="button"
                    >
                        <UserCheck size={13} aria-hidden="true" />
                        Assign
                      </button>
                    </td>
                  </tr>
                ))
              ) : (
                <tr>
                  <td colSpan={4} style={{ textAlign: "center", padding: "24px 0" }}>
                    No {role.toLowerCase()} found.
                  </td>
                </tr>
              )}
            </tbody>
          </table>
        </div>
      </div>
    </Modal>
  );
};
