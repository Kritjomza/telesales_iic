import { useState, useEffect } from "react";
import { apiService, setUnauthorizedCallback, setForbiddenCallback } from "./domain/apiService";
import {
  BarChart3,
  BookOpen,
  LayoutDashboard,
  LogOut,
  Search,
  Table2,
  Users,
  Building2
} from "lucide-react";
import { Toast, type ToastItem } from "./components/Toast";
import { CustomerManageView } from "./views/CustomerManageView";
import { BookingView } from "./views/BookingView";
import { CostSheetView } from "./views/CostSheetView";
import { ReportsView } from "./views/ReportsView";
import { MasterDataView, type MasterTableType } from "./views/MasterDataView";
import { LoginView } from "./views/LoginView";
import { ForbiddenView } from "./views/ForbiddenView";

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
      { name: "Manage", key: "manage" },
      { name: "Booking", key: "booking" }
    ]
  },
  {
    label: "Report",
    icon: BookOpen,
    items: [
      { name: "Operation", key: "reports" },
      { name: "Assign History", key: "reports" },
      { name: "Performance", key: "reports" },
      { name: "Summary Renewal", key: "reports" },
      { name: "Summary Project Detail", key: "reports" }
    ]
  },
  {
    label: "Sale Manager",
    icon: BarChart3,
    items: [
      { name: "Cost Sheet", key: "cost-sheet" }
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
    "Sale Manager": false
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

  const showToast = (message: string, type: "success" | "error" | "info") => {
    const newToast: ToastItem = {
      id: Math.random().toString(36).substring(2, 9),
      message,
      type
    };
    setToasts((prev) => [...prev, newToast]);
  };

  const removeToast = (id: string) => {
    setToasts((prev) => prev.filter((t) => t.id !== id));
  };

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
          background: "#f8fafc",
          color: "var(--text-muted)",
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

  const canAccessGroup = (groupLabel: string, role: string) => {
    const isAdmin = role === "Admin" || role === "Super Admin";
    const isSupervisor = role === "Manager" || role === "Supervisor";
    const isAgent = role === "Sale" || role === "Tele sale" || role === "Tele Sale";
    const isViewer = role === "Viewer";

    if (groupLabel === "Master Data") {
      return isAdmin;
    }
    if (groupLabel === "Customer") {
      return isAdmin || isSupervisor || isAgent || isViewer;
    }
    if (groupLabel === "Report") {
      return isAdmin || isSupervisor || isViewer;
    }
    if (groupLabel === "Sale Manager") {
      return isAdmin || isSupervisor || isAgent;
    }
    return false;
  };

  const canAccessView = (viewKey: string, role: string) => {
    if (viewKey === "forbidden") {
      return true;
    }
    if (viewKey === "manage" || viewKey === "booking") {
      return canAccessGroup("Customer", role);
    }
    if (viewKey === "cost-sheet") {
      return canAccessGroup("Sale Manager", role);
    }
    if (viewKey === "reports") {
      return canAccessGroup("Report", role);
    }
    if (viewKey === "master-data") {
      return canAccessGroup("Master Data", role);
    }
    return false;
  };

  return (
    <div className="app-shell">
      {/* Sidebar Navigation */}
      <aside className="sidebar">
        <a className="brand" href="/" onClick={(e) => { e.preventDefault(); setCurrentView("manage"); }} aria-label="ATS Home">
          <span className="brand-mark">ATS</span>
          <span>
            <strong>ATS</strong>
            <small>Sale Management</small>
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
              return (
                <section className="nav-group" key={group.label}>
                  <button
                    className="nav-parent"
                    type="button"
                  >
                    <Icon size={17} />
                    <span>{group.label}</span>
                  </button>

                  <div className="nav-children">
                    {group.items.map((item) => {
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
            {currentView === "booking" && (
              <BookingView userRole={currentUser.roles} showToast={showToast} />
            )}
            {currentView === "cost-sheet" && (
              <CostSheetView userRole={currentUser.roles} showToast={showToast} />
            )}
            {currentView === "reports" && (
              <ReportsView />
            )}
            {currentView === "master-data" && (
              <MasterDataView tableType={activeMasterTable} showToast={showToast} />
            )}
          </>
        )}
      </div>

      {/* Global Toast Notifications */}
      <Toast toasts={toasts} onClose={removeToast} />
    </div>
  );
}

export default App;
