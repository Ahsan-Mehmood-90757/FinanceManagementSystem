// Adapted from React Bits "CountUp" (https://reactbits.dev, MIT). Spring-animated number that counts to `to`.
import { useEffect, useRef } from "react";
import { useInView, useMotionValue, useSpring } from "motion/react";

interface CountUpProps {
  to: number;
  from?: number;
  duration?: number;
  decimals?: number;
  className?: string;
  formatter?: (n: number) => string;
}

export default function CountUp({ to, from = 0, duration = 1.2, decimals = 0, className, formatter }: CountUpProps) {
  const ref = useRef<HTMLSpanElement>(null);
  const value = useMotionValue(from);
  const damping = 20 + 40 * (1 / duration);
  const stiffness = 100 * (1 / duration);
  const spring = useSpring(value, { damping, stiffness });
  const inView = useInView(ref, { once: true, margin: "0px" });

  const format = (n: number) =>
    formatter
      ? formatter(n)
      : n.toLocaleString("en-PK", { minimumFractionDigits: decimals, maximumFractionDigits: decimals });

  useEffect(() => {
    if (ref.current) ref.current.textContent = format(from);
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [from]);

  useEffect(() => {
    if (inView) value.set(to);
  }, [inView, to, value]);

  useEffect(
    () =>
      spring.on("change", (latest) => {
        if (ref.current) ref.current.textContent = format(latest);
      }),
    // eslint-disable-next-line react-hooks/exhaustive-deps
    [spring, decimals, formatter],
  );

  return <span ref={ref} className={className} />;
}
