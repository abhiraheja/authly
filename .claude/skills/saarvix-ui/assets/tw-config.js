/* Tailwind Play CDN config — load AFTER the tailwind CDN script.
   Maps SAARVIX semantic tokens onto Tailwind color utilities so
   classes like bg-primary, text-muted-foreground, border-border,
   and opacity variants (bg-primary/10) all work. */
tailwind.config = {
  darkMode: "class",
  theme: {
    extend: {
      colors: {
        background: "rgb(var(--background) / <alpha-value>)",
        foreground: "rgb(var(--foreground) / <alpha-value>)",
        card: "rgb(var(--card) / <alpha-value>)",
        "card-foreground": "rgb(var(--card-foreground) / <alpha-value>)",
        popover: "rgb(var(--popover) / <alpha-value>)",
        primary: "rgb(var(--primary) / <alpha-value>)",
        "primary-foreground": "rgb(var(--primary-foreground) / <alpha-value>)",
        secondary: "rgb(var(--secondary) / <alpha-value>)",
        "secondary-foreground": "rgb(var(--secondary-foreground) / <alpha-value>)",
        muted: "rgb(var(--muted) / <alpha-value>)",
        "muted-foreground": "rgb(var(--muted-foreground) / <alpha-value>)",
        accent: "rgb(var(--accent) / <alpha-value>)",
        "accent-foreground": "rgb(var(--accent-foreground) / <alpha-value>)",
        success: "rgb(var(--success) / <alpha-value>)",
        warning: "rgb(var(--warning) / <alpha-value>)",
        danger: "rgb(var(--danger) / <alpha-value>)",
        info: "rgb(var(--info) / <alpha-value>)",
        border: "rgb(var(--border) / <alpha-value>)",
        input: "rgb(var(--input) / <alpha-value>)",
        ring: "rgb(var(--ring) / <alpha-value>)",
        ink: "rgb(var(--ink) / <alpha-value>)",
        teal: "rgb(var(--teal) / <alpha-value>)",
        mint: "rgb(var(--mint) / <alpha-value>)",
        coral: "rgb(var(--coral) / <alpha-value>)",
        sidebar: "rgb(var(--sidebar) / <alpha-value>)",
        "sidebar-foreground": "rgb(var(--sidebar-foreground) / <alpha-value>)",
        "sidebar-accent": "rgb(var(--sidebar-accent) / <alpha-value>)",
        "sidebar-border": "rgb(var(--sidebar-border) / <alpha-value>)",
      },
      fontFamily: {
        sans: ['-apple-system','BlinkMacSystemFont','"SF Pro Display"','"SF Pro Text"','Inter','ui-sans-serif','system-ui','sans-serif'],
        display: ['-apple-system','BlinkMacSystemFont','"SF Pro Display"','Inter','ui-sans-serif','system-ui','sans-serif'],
      },
      // Tight enterprise corners (≤8px). Overrides defaults so inline
      // rounded-lg / rounded-xl / rounded-2xl in markup shrink too.
      borderRadius: {
        none: "0", sm: "2px", DEFAULT: "4px", md: "4px",
        lg: "6px", xl: "8px", "2xl": "8px", "3xl": "10px", full: "9999px",
      },
      boxShadow: {
        sm: "var(--shadow-sm)", DEFAULT: "var(--shadow)",
        md: "var(--shadow-md)", lg: "var(--shadow-lg)",
      },
    },
  },
};
