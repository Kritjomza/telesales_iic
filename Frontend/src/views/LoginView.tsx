import React, { useState } from "react";
import { Lock, User, AlertCircle, Eye, EyeOff, ShieldCheck, Server } from "lucide-react";
import { apiService } from "../domain/apiService";

import logo from "../assets/logo.jpg";

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
    <div className="login-container">
      <section className="login-shell" aria-label="IIC telesales sign in">
        <aside className="login-brand-panel">
          <div className="login-brand-mark" style={{ overflow: "hidden" }}>
            <img src={logo} alt="IIC Logo" style={{ width: "100%", height: "100%", objectFit: "cover", borderRadius: "inherit" }} />
          </div>
          <div>
            <p className="login-kicker">Secure operations access</p>
            <h1>IIC Telesales Platform</h1>
            <p className="login-brand-copy">
              A controlled workspace for customer management, import review, and telesales execution.
            </p>
          </div>
          <div className="login-trust-row" aria-label="Platform trust indicators">
            <span><ShieldCheck size={15} /> Role-based access</span>
            <span><Server size={15} /> Live customer data</span>
          </div>
        </aside>

        <div className="login-card">
          <div className="login-card-header">
            <div className="login-card-mark" aria-hidden="true">
              <Lock size={18} />
            </div>
            <div>
              <h2>Sign in</h2>
              <p>IIC Telesales Modernization System</p>
            </div>
          </div>

          {errorMsg && (
            <div className="login-alert" role="alert">
              <AlertCircle size={16} />
              <span>{errorMsg}</span>
            </div>
          )}

          <form onSubmit={handleSubmit} className="login-form">
            <div className="form-group">
              <label htmlFor="username_login">
                Username
              </label>
              <div className="login-input-wrap">
                <User size={16} className="login-input-icon" />
                <input
                  id="username_login"
                  type="text"
                  placeholder="Enter your username"
                  value={username}
                  onChange={(e) => setUsername(e.target.value)}
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
              <div className="login-input-wrap">
                <Lock size={16} className="login-input-icon" />
                <input
                  id="password_login"
                  type={showPassword ? "text" : "password"}
                  placeholder="Enter your password"
                  value={password}
                  onChange={(e) => setPassword(e.target.value)}
                  disabled={isLoading}
                  autoComplete="current-password"
                  required
                />
                <button
                  type="button"
                  className="login-password-toggle"
                  onClick={() => setShowPassword(!showPassword)}
                  aria-label={showPassword ? "Hide password" : "Show password"}
                >
                  {showPassword ? <EyeOff size={16} /> : <Eye size={16} />}
                </button>
              </div>
            </div>

            <button
              type="submit"
              className="primary-button login-submit"
              disabled={isLoading}
            >
              {isLoading ? "Signing in..." : "Sign In"}
            </button>
          </form>
        </div>
      </section>
    </div>
  );
};
