import { useState, useEffect, useCallback } from "react";
import { apiService, setUnauthorizedCallback, setForbiddenCallback } from "./domain/apiService";
import {
  BookOpen,
  LogOut,
  Table2,
  Users,
  Building2,
  Database
} from "lucide-react";
import { Toast, type ToastItem } from "./components/Toast";
import { AiChatWidget } from "./components/AiChatWidget";
import { CustomerManageView } from "./views/CustomerManageView";
import { CostSheetView } from "./views/CostSheetView";
import { ReportsView } from "./views/ReportsView";
import { MasterDataView, type MasterTableType } from "./views/MasterDataView";
import { LoginView } from "./views/LoginView";
import { ForbiddenView } from "./views/ForbiddenView";
import { ImportCustomersView } from "./views/ImportCustomersView";
import { ImportHistoryView } from "./views/ImportHistoryView";
import { canAccessGroup, canAccessView, normalizeRole } from "./domain/permissions";
const navigationGroups = [
  {
    label: "Master Data",
    icon: Table2,
    items: [
      { name: "Profiles", key: "profiles" },
      { name: "Antivirus Price List", key: "antiviruspricelist" },
      { name: "Product", key: "products" },
      { name: "Brand", key: "brands" },
      { name: "Business Type", key: "businesstypes" },
      { name: "Category", key: "categories" },
      { name: "User", key: "users" },
      { name: "Competitor", key: "competitors" }
    ]
  },
  {
    label: "Customer",
    icon: Users,
    items: [
      { name: "Manage", key: "manage" }
    ]
  },
  {
    label: "Sale Manager",
    icon: Building2,
    items: [
      { name: "Cost Sheet", key: "cost-sheet" }
    ]
  },
  {
    label: "Report",
    icon: BookOpen,
    items: [
      { name: "Operation", key: "reports" },
      { name: "Summary Renewal", key: "reports" },
      { name: "Summary Project Detail", key: "reports" }
    ]
  },
  {
    label: "Admin",
    icon: Database,
    items: [
      { name: "Import Data", key: "import-customers" },
      { name: "Import History", key: "import-history" }
    ]
  }
];
function App() {
  // Navigation State
  const [currentView, setCurrentView] = useState<string>("manage");
  const [activeMasterTable, setActiveMasterTable] = useState<MasterTableType>("profiles");
  const [expandedGroups, setExpandedGroups] = useState<Record<string, boolean>>({
    "Master Data": false,
    "Customer": true,
    "Report": false,
    "Sale Manager": false,
    "Admin": false
  });
  // User Session
  const [currentUser, setCurrentUser] = useState<{
    id: number;
    username: string;
    name: string;
    email: string;
    roles: string;
    avatar: string;
  } | null>(() => {
    const saved = localStorage.getItem("ats_user");
    return saved ? JSON.parse(saved) : null;
  });
  const [isLoadingSession, setIsLoadingSession] = useState(true);
  // Global Toast State
  const [toasts, setToasts] = useState<ToastItem[]>([]);
  const showToast = useCallback((message: string, type: "success" | "error" | "info") => {
    const newToast: ToastItem = {
      id: Math.random().toString(36).substring(2, 9),
      message,
      type
    };
    setToasts((prev) => [...prev, newToast]);
  }, []);
  const removeToast = useCallback((id: string) => {
    setToasts((prev) => prev.filter((t) => t.id !== id));
  }, []);
  // Setup 401/403 callbacks and check session on mount
  useEffect(() => {
    setUnauthorizedCallback(() => {
      localStorage.removeItem("ats_user");
      setCurrentUser(null);
      showToast("Session expired. Please sign in again.", "error");
    });
    setForbiddenCallback(() => {
      setCurrentView("forbidden");
      showToast("Access denied. You do not have permission to perform this action.", "error");
    });
    const checkSession = async () => {
      try {
        const user = await apiService.getMe();
        setCurrentUser(user);
        localStorage.setItem("ats_user", JSON.stringify(user));
      } catch (err) {
        localStorage.removeItem("ats_user");
        setCurrentUser(null);
      } finally {
        setIsLoadingSession(false);
      }
    };
    checkSession();
  }, []);
  const toggleGroup = (groupLabel: string) => {
    setExpandedGroups(prev => ({
      ...prev,
      [groupLabel]: !prev[groupLabel]
    }));
  };
  const handleNavigation = (viewKey: string, itemName: string) => {
    setCurrentView(viewKey);
    if (viewKey === "master-data") {
      // Find the corresponding master table key
      const match = navigationGroups[0].items.find(x => x.name === itemName);
      if (match) {
        setActiveMasterTable(match.key as MasterTableType);
      }
    }
  };
  const isChildSelected = (itemName: string, viewKey: string) => {
    if (currentView === "master-data" && viewKey === "master-data") {
      const match = navigationGroups[0].items.find(x => x.name === itemName);
      return match && activeMasterTable === match.key;
    }
    if (currentView === "reports" && itemName === "Operation") return true; // Default
    return currentView === viewKey && currentView !== "master-data";
  };
  if (isLoadingSession) {
    return (
      <div
        style={{
          display: "flex",
          alignItems: "center",
          justifyContent: "center",
          minHeight: "100vh",
          background: "var(--iic-dark)",
          color: "var(--iic-muted)",
          fontFamily: "var(--font-family)",
          fontSize: "14px"
        }}
      >
        Loading session...
      </div>
    );
  }
  if (!currentUser) {
    return (
      <div className="app-login-container">
        <LoginView
          onLoginSuccess={(user) => setCurrentUser(user)}
          showToast={showToast}
        />
        <Toast toasts={toasts} onClose={removeToast} />
      </div>
    );
  }
  return (
    <div className="app-shell">
      {/* Sidebar Navigation */}
      <aside className="sidebar">
        <a className="brand" href="/" onClick={(e) => { e.preventDefault(); setCurrentView("manage"); }} aria-label="IIC Telesales home">
          <span className="brand-mark">IIC</span>
          <span>
            <strong>IIC Telesales</strong>
            <small>Modernization Console</small>
          </span>
        </a>

        {/* User Card */}
        <div className="user-card">
          <div className="avatar">{currentUser.avatar}</div>
          <div>
            <strong>{currentUser.name}</strong>
            <span>{currentUser.roles}</span>
          </div>
        </div>

        {/* Navigation list */}
        <nav aria-label="Primary navigation" className="nav-groups">
          {navigationGroups
            .filter((group) => canAccessGroup(group.label, currentUser.roles))
            .map((group) => {
              const Icon = group.icon;
              const groupActive = group.items.some((item) => {
                const viewKey = group.label === "Master Data" ? "master-data" : item.key;
                return Boolean(isChildSelected(item.name, viewKey));
              });
              return (
                <section className="nav-group" key={group.label}>
                  <button
                    className={`nav-parent ${groupActive ? "active" : ""}`}
                    type="button"
                    aria-current={groupActive ? "page" : undefined}
                  >
                    <Icon size={17} />
                    <span>{group.label}</span>
                  </button>
                  <div className="nav-children">
                    {group.items
                      .filter(item => {
                        const viewKey = group.label === "Master Data" ? "master-data" : item.key;
                        if (!canAccessView(viewKey, currentUser.roles)) {
                          return false;
                        }
                        if (group.label === "Master Data" && normalizeRole(currentUser.roles) === "Manager") {
                          return item.name === "Brand" || item.name === "Product";
                        }
                        return true;
                      })
                      .map((item) => {
                        const viewKey = group.label === "Master Data" ? "master-data" : item.key;
                        const selected = isChildSelected(item.name, viewKey);
                        return (
                          <button
                            key={item.name}
                            className={`nav-child-btn ${selected ? "selected" : ""}`}
                            onClick={() => handleNavigation(viewKey, item.name)}
                            type="button"
                          >
                            {item.name}
                          </button>
                        );
                      })}
                  </div>
                </section>
              );
            })}
        </nav>

        {/* Logout Button */}
        <button
          className="logout-btn"
          onClick={async () => {
            showToast("Signing out...", "info");
            try {
              await apiService.logout();
            } catch (err) {
              console.error("Logout failed on server", err);
            }
            localStorage.removeItem("ats_user");
            setTimeout(() => {
              setCurrentUser(null);
            }, 500);
          }}
          type="button"
        >
          <LogOut size={16} />
          <span>Sign Out</span>
        </button>
      </aside>

      {/* Main Workspace */}
      <div className="workspace">
        {/* Render View */}
        {!canAccessView(currentView, currentUser.roles) ? (
          <ForbiddenView />
        ) : (
          <>
            {currentView === "forbidden" && (
              <ForbiddenView />
            )}
            {currentView === "manage" && (
              <CustomerManageView userRole={currentUser.roles} showToast={showToast} />
            )}

            {currentView === "cost-sheet" && (
              <CostSheetView userRole={currentUser.roles} showToast={showToast} />
            )}
            {currentView === "reports" && (
              <ReportsView />
            )}
            {currentView === "master-data" && (
              <MasterDataView tableType={activeMasterTable} userRole={currentUser.roles} showToast={showToast} />
            )}
            {currentView === "import-customers" && (
              <ImportCustomersView showToast={showToast} />
            )}
            {currentView === "import-history" && (
              <ImportHistoryView userRole={currentUser.roles} showToast={showToast} />
            )}
          </>
        )}
      </div>

      <AiChatWidget />

      {/* Global Toast Notifications */}
      <Toast toasts={toasts} onClose={removeToast} />
    </div>
  );
}
export default App;
