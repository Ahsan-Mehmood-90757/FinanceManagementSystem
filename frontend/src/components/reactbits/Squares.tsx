// Adapted from React Bits "Squares" (https://reactbits.dev, MIT). A slowly drifting grid with hover highlight,
// tuned here to read as ruled ledger paper.
import { useEffect, useRef } from "react";

type Direction = "diagonal" | "up" | "right" | "down" | "left";

interface SquaresProps {
  direction?: Direction;
  speed?: number;
  borderColor?: string;
  squareSize?: number;
  hoverFillColor?: string;
  className?: string;
}

export default function Squares({
  direction = "diagonal",
  speed = 0.25,
  borderColor = "rgba(15,118,110,0.12)",
  squareSize = 36,
  hoverFillColor = "rgba(15,118,110,0.08)",
  className = "",
}: SquaresProps) {
  const canvasRef = useRef<HTMLCanvasElement>(null);

  useEffect(() => {
    const canvas = canvasRef.current;
    if (!canvas) return;
    const ctx = canvas.getContext("2d");
    if (!ctx) return;

    const reduced = window.matchMedia("(prefers-reduced-motion: reduce)").matches;
    let frame = 0;
    const offset = { x: 0, y: 0 };
    let hovered: { x: number; y: number } | null = null;

    const resize = () => {
      const dpr = window.devicePixelRatio || 1;
      canvas.width = canvas.offsetWidth * dpr;
      canvas.height = canvas.offsetHeight * dpr;
      ctx.setTransform(dpr, 0, 0, dpr, 0, 0);
    };

    const draw = () => {
      const w = canvas.offsetWidth;
      const h = canvas.offsetHeight;
      ctx.clearRect(0, 0, w, h);
      const startX = Math.floor(offset.x / squareSize) * squareSize;
      const startY = Math.floor(offset.y / squareSize) * squareSize;
      ctx.lineWidth = 1;
      for (let x = startX; x < w + squareSize; x += squareSize) {
        for (let y = startY; y < h + squareSize; y += squareSize) {
          const sx = x - (offset.x % squareSize);
          const sy = y - (offset.y % squareSize);
          if (hovered && Math.floor((x - startX) / squareSize) === hovered.x && Math.floor((y - startY) / squareSize) === hovered.y) {
            ctx.fillStyle = hoverFillColor;
            ctx.fillRect(sx, sy, squareSize, squareSize);
          }
          ctx.strokeStyle = borderColor;
          ctx.strokeRect(sx + 0.5, sy + 0.5, squareSize, squareSize);
        }
      }
      // Fade the grid into the page towards the edges.
      const g = ctx.createRadialGradient(w / 2, h / 2, 0, w / 2, h / 2, Math.hypot(w, h) / 2);
      g.addColorStop(0, "rgba(248,250,252,0)");
      g.addColorStop(1, "rgba(248,250,252,1)");
      ctx.fillStyle = g;
      ctx.fillRect(0, 0, w, h);
    };

    const tick = () => {
      const s = Math.max(speed, 0.05);
      if (direction === "right" || direction === "diagonal") offset.x = (offset.x - s + squareSize) % squareSize;
      if (direction === "left") offset.x = (offset.x + s + squareSize) % squareSize;
      if (direction === "up") offset.y = (offset.y + s + squareSize) % squareSize;
      if (direction === "down" || direction === "diagonal") offset.y = (offset.y - s + squareSize) % squareSize;
      draw();
      frame = requestAnimationFrame(tick);
    };

    const onMove = (e: MouseEvent) => {
      const rect = canvas.getBoundingClientRect();
      const mx = e.clientX - rect.left;
      const my = e.clientY - rect.top;
      const startX = Math.floor(offset.x / squareSize) * squareSize;
      const startY = Math.floor(offset.y / squareSize) * squareSize;
      hovered = {
        x: Math.floor((mx + (offset.x % squareSize) - startX) / squareSize),
        y: Math.floor((my + (offset.y % squareSize) - startY) / squareSize),
      };
      if (reduced) draw();
    };
    const onLeave = () => {
      hovered = null;
      if (reduced) draw();
    };

    resize();
    window.addEventListener("resize", resize);
    canvas.addEventListener("mousemove", onMove);
    canvas.addEventListener("mouseleave", onLeave);
    if (reduced) draw();
    else frame = requestAnimationFrame(tick);

    return () => {
      cancelAnimationFrame(frame);
      window.removeEventListener("resize", resize);
      canvas.removeEventListener("mousemove", onMove);
      canvas.removeEventListener("mouseleave", onLeave);
    };
  }, [direction, speed, borderColor, squareSize, hoverFillColor]);

  return <canvas ref={canvasRef} className={`block h-full w-full ${className}`} aria-hidden />;
}
