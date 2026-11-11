// Adapted from React Bits "ShinyText" (https://reactbits.dev, MIT). A slow highlight sweeping across text.
interface ShinyTextProps {
  text: string;
  disabled?: boolean;
  speed?: number;
  className?: string;
  baseColor?: string;
  shineColor?: string;
}

export default function ShinyText({
  text,
  disabled = false,
  speed = 4,
  className = "",
  baseColor = "var(--color-accent)",
  shineColor = "var(--color-shine)",
}: ShinyTextProps) {
  return (
    <span
      className={`inline-block ${className}`}
      style={{
        backgroundImage: `linear-gradient(110deg, ${baseColor} 40%, ${shineColor} 50%, ${baseColor} 60%)`,
        backgroundSize: "220% 100%",
        backgroundClip: "text",
        WebkitBackgroundClip: "text",
        color: "transparent",
        animation: disabled ? undefined : `rb-shine ${speed}s linear infinite`,
      }}
    >
      <style>{`@keyframes rb-shine { 0% { background-position: 120% 0 } 100% { background-position: -120% 0 } }`}</style>
      {text}
    </span>
  );
}
