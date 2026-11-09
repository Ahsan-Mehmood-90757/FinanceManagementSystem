// Adapted from React Bits "BlurText" (https://reactbits.dev, MIT). Words resolve from a blur, one after another.
import { motion } from "motion/react";

interface BlurTextProps {
  text: string;
  delay?: number;
  className?: string;
  as?: "h1" | "h2" | "p";
}

export default function BlurText({ text, delay = 0.08, className = "", as = "h1" }: BlurTextProps) {
  const Tag = motion[as];
  const words = text.split(" ");
  return (
    <Tag className={className} aria-label={text}>
      {words.map((word, i) => (
        <motion.span
          key={`${word}-${i}`}
          aria-hidden
          className="inline-block will-change-[filter,transform,opacity]"
          initial={{ filter: "blur(10px)", opacity: 0, y: -6 }}
          animate={{ filter: "blur(0px)", opacity: 1, y: 0 }}
          transition={{ duration: 0.5, delay: i * delay, ease: [0.22, 1, 0.36, 1] }}
        >
          {word}
          {i < words.length - 1 && " "}
        </motion.span>
      ))}
    </Tag>
  );
}
