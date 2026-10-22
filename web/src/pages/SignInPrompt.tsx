export default function SignInPrompt({ what, onLogin }: { what: string; onLogin: () => void }) {
  return (
    <section className="card page-card">
      <p>Sign in to {what}.</p>
      <button className="btn btn-primary" onClick={onLogin}>
        Sign in
      </button>
    </section>
  );
}
