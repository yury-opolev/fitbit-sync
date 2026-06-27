namespace FitbitSync.Host;

// How `login` runs. Interactive is the existing desktop flow (system browser + loopback listener).
// Begin/Complete are the headless, agent-driven steps that emit a JSON envelope.
public enum LoginMode
{
    Interactive,
    Begin,
    Complete,
}
