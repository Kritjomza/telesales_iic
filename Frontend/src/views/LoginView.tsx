import React, { useState } from "react";
import { Lock, User, AlertCircle, Eye, EyeOff } from "lucide-react";
import { apiService } from "../domain/apiService";

interface LoginViewProps {
  onLoginSuccess: (user: any) => void;
  showToast: (message: string, type: "success" | "error" | "info") => void;
}

export const LoginView: React.FC<LoginViewProps> = ({ onLoginSuccess, showToast }) => {
  const [username, setUsername] = useState("");
  const [password, setPassword] = useState("");
  const [showPassword, setShowPassword] = useState(false);
  const [isLoading, setIsLoading] = useState(false);
  const [errorMsg, setErrorMsg] = useState<string | null>(null);

  const handleSubmit = async (e: React.FormEvent) => {
    e.preventDefault();
    if (!username.trim() || !password.trim()) {
      setErrorMsg("Please fill in all fields.");
      return;
    }

    setIsLoading(true);
    setErrorMsg(null);

    try {
      const userProfile = await apiService.login(username.trim(), password);
      showToast(`Welcome back, ${userProfile.name}!`, "success");
      onLoginSuccess(userProfile);
    } catch (err: any) {
      setErrorMsg(err.message || "Invalid username or password");
      showToast("Authentication failed", "error");
    } finally {
      setIsLoading(false);
    }
  };

  return (
    <div 
      className="login-container" 
      style={{
        display: "flex",
        alignItems: "center",
        justifyContent: "center",
        minHeight: "100vh",
        background: "linear-gradient(135deg, #020617 0%, #071426 46%, #0a2a52 100%)",
        padding: "20px"
      }}
    >
      <div 
        className="login-card" 
        style={{
          width: "100%",
          maxWidth: "400px",
          background: "var(--iic-navy)",
          border: "1px solid var(--iic-border)",
          borderRadius: "8px",
          boxShadow: "var(--shadow-lg)",
          padding: "32px",
          display: "flex",
          flexDirection: "column",
          gap: "24px"
        }}
      >
        <div style={{ textAlign: "center" }}>
          <div 
            style={{
              width: "48px",
              height: "48px",
              background: "var(--iic-blue)",
              color: "#ffffff",
              borderRadius: "8px",
              display: "grid",
              placeItems: "center",
              margin: "0 auto 16px auto",
              fontWeight: 800,
              fontSize: "18px"
            }}
          >
            IIC
          </div>
          <h1 style={{ fontSize: "20px", fontWeight: 700, color: "var(--iic-text)" }}>Sign In</h1>
          <p style={{ fontSize: "13px", color: "var(--iic-muted)", marginTop: "4px" }}>
            IIC Telesales Modernization System
          </p>
        </div>

        {errorMsg && (
          <div 
            style={{
              display: "flex",
              alignItems: "center",
              gap: "8px",
              padding: "10px 12px",
              background: "var(--iic-danger-bg)",
              border: "1px solid var(--iic-danger-border)",
              borderRadius: "6px",
              color: "var(--iic-danger)",
              fontSize: "13px"
            }}
          >
            <AlertCircle size={16} style={{ flexShrink: 0 }} />
            <span>{errorMsg}</span>
          </div>
        )}

        <form onSubmit={handleSubmit} style={{ display: "flex", flexDirection: "column", gap: "16px" }}>
          <div className="form-group">
            <label htmlFor="username_login">
              Username
            </label>
            <div style={{ position: "relative" }}>
              <User 
                size={16} 
                style={{
                  position: "absolute",
                  left: "12px",
                  top: "50%",
                  transform: "translateY(-50%)",
                  color: "var(--text-light)"
                }} 
              />
              <input
                id="username_login"
                type="text"
                placeholder="Enter your username"
                value={username}
                onChange={(e) => setUsername(e.target.value)}
                style={{
                  width: "100%",
                  padding: "10px 12px 10px 36px"
                }}
                disabled={isLoading}
                autoComplete="username"
                required
              />
            </div>
          </div>

          <div className="form-group">
            <label htmlFor="password_login">
              Password
            </label>
            <div style={{ position: "relative" }}>
              <Lock 
                size={16} 
                style={{
                  position: "absolute",
                  left: "12px",
                  top: "50%",
                  transform: "translateY(-50%)",
                  color: "var(--text-light)"
                }} 
              />
              <input
                id="password_login"
                type={showPassword ? "text" : "password"}
                placeholder="Enter your password"
                value={password}
                onChange={(e) => setPassword(e.target.value)}
                style={{
                  width: "100%",
                  padding: "10px 36px 10px 36px"
                }}
                disabled={isLoading}
                autoComplete="current-password"
                required
              />
              <button
                type="button"
                onClick={() => setShowPassword(!showPassword)}
                style={{
                  position: "absolute",
                  right: "12px",
                  top: "50%",
                  transform: "translateY(-50%)",
                  background: "transparent",
                  border: 0,
                  cursor: "pointer",
                  color: "var(--text-light)",
                  display: "grid",
                  placeItems: "center",
                  padding: 0
                }}
                aria-label={showPassword ? "Hide password" : "Show password"}
              >
                {showPassword ? <EyeOff size={16} /> : <Eye size={16} />}
              </button>
            </div>
          </div>

          <button
            type="submit"
            className="primary-button"
            style={{
              width: "100%",
              height: "42px",
              fontSize: "14px",
              fontWeight: 700,
              marginTop: "8px"
            }}
            disabled={isLoading}
          >
            {isLoading ? "Signing in..." : "Sign In"}
          </button>
        </form>
      </div>
    </div>
  );
};
