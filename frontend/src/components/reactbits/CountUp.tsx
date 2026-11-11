// Adapted from React Bits "CountUp" (https://reactbits.dev, MIT). Counts to `to` once it scrolls into view.
// Uses a fixed-length eased tween rather than a spring: a spring's settle time grows with the size of the number,
// so large amounts would crawl through the last few digits for many seconds.
import { useEffect, useRef } from "react";
import { animate, useInView } from "motion/react";

interface CountUpProps {
  to: number;
  from?: number;
  /** Seconds. */
  duration?: number;
  decimals?: number;
  className?: string;
  formatter?: (n: number) => string;
}

export default function CountUp({ to, from = 0, duration = 1, decimals = 0, className, formatter }: CountUpProps) {
  const ref = useRef<HTMLSpanElement>(null);
  const shown = useRef(from);
  const inView = useInView(ref, { once: true, margin: "0px" });

  const format = (n: number) =>
    formatter
      ? formatter(n)
      : n.toLocaleString("en-PK", { minimumFractionDigits: decimals, maximumFractionDigits: decimals });

  useEffect(() => {
    const el = ref.current;
    if (!el) return;
    if (!inView) {
      el.textContent = format(shown.current);
      return;
    }
    // Animate from whatever is on screen, so a refetch glides to the new value instead of restarting at zero.
    const controls = animate(shown.current, to, {
      duration,
      ease: [0.16, 1, 0.3, 1], // easeOutExpo: fast start, gentle landing
      onUpdate: (n) => {
        shown.current = n;
        el.textContent = format(n);
      },
    });
    return () => controls.stop();
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [inView, to, duration, decimals, formatter]);

  return <span ref={ref} className={className} />;
}
