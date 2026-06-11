import React from "react";
import { ShieldAlert } from "lucide-react";

export const ForbiddenView: React.FC = () => {
  return (
    <div 
      className="forbidden-view animate-fade-in"
      style={{
        display: "flex",
        alignItems: "center",
        justifyContent: "center",
        minHeight: "calc(100vh - 80px)",
        padding: "40px",
        background: "var(--surface-alt)",
        fontFamily: "var(--font-family)"
      }}
    >
      <div 
        style={{
          maxWidth: "480px",
          width: "100%",
          background: "var(--surface)",
          border: "1px solid var(--border)",
          borderRadius: "var(--rounded-lg)",
          boxShadow: "var(--shadow)",
          padding: "32px",
          textAlign: "center"
        }}
      >
        <div 
          style={{
            display: "inline-flex",
            alignItems: "center",
            justifyContent: "center",
            width: "56px",
            height: "56px",
            borderRadius: "50%",
            background: "var(--danger-light)",
            color: "var(--danger)",
            marginBottom: "20px"
          }}
        >
          <ShieldAlert size={28} />
        </div>
        <h2 
          style={{
            fontSize: "18px",
            fontWeight: 700,
            color: "var(--primary)",
            marginBottom: "8px"
          }}
        >
          Access Denied
        </h2>
        <p 
          style={{
            fontSize: "13px",
            color: "var(--text-muted)",
            lineHeight: "1.6",
            marginBottom: "24px"
          }}
        >
          You do not have the required permissions to view this resource or perform this action. If you believe this is an error, please contact your administrator.
        </p>
        <button 
          className="primary-button" 
          onClick={() => window.location.reload()}
          style={{
            width: "100%",
            justifyContent: "center"
          }}
          type="button"
        >
          Reload Console
        </button>
      </div>
    </div>
  );
};
