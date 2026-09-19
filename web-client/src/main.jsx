import React, { useEffect, useState } from "react";
import { createRoot } from "react-dom/client";
import "./styles.css";

function App() {
  const [server, setServer] = useState({ state: "checking" });

  useEffect(() => {
    fetch("/api/v1/server/info")
      .then((response) => {
        if (!response.ok) throw new Error(`HTTP ${response.status}`);
        return response.json();
      })
      .then((data) => setServer({ state: "ready", data }))
      .catch((error) => setServer({ state: "offline", error: String(error) }));
  }, []);

  return (
    <main className="shell">
      <section className="hero">
        <div className="eyebrow">INTER-LAN · WEB CLIENT</div>
        <h1>Communication stays on your LAN.</h1>
        <p className="lede">
          Android uses this responsive client. The owner machine remains the canonical server.
        </p>

        <div className="status-card">
          <span className={`dot ${server.state}`} />
          <div>
            <strong>
              {server.state === "ready"
                ? server.data.configured
                  ? server.data.serverName
                  : "Server not configured"
                : server.state === "offline"
                  ? "Server unavailable"
                  : "Checking server"}
            </strong>
            <p>
              {server.state === "ready"
                ? `API ${server.data.apiVersion} · ${server.data.runtimeMode}`
                : server.state === "offline"
                  ? server.error
                  : "Reading /api/v1/server/info"}
            </p>
          </div>
        </div>

        <div className="actions">
          <button disabled>Create Server</button>
          <button disabled className="secondary">Join Server</button>
        </div>
        <small>P0 shell only. Enrollment and messaging are implemented in later bounded phases.</small>
      </section>
    </main>
  );
}

createRoot(document.getElementById("root")).render(
  <React.StrictMode>
    <App />
  </React.StrictMode>
);
