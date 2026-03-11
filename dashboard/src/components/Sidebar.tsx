"use client";

import Link from "next/link";
import { usePathname } from "next/navigation";

const navItems = [
  { href: "/", label: "Home", icon: "🏠" },
  { href: "/cameras", label: "Cameras", icon: "📷" },
  { href: "/zones", label: "Zones", icon: "📐" },
  { href: "/rules", label: "Rules", icon: "⚙️" },
  { href: "/notifications", label: "Notifications", icon: "🔔" },
  { href: "/events", label: "Events", icon: "📋" },
  { href: "/settings", label: "Settings", icon: "🛠️" },
];

export function Sidebar() {
  const pathname = usePathname();

  return (
    <aside className="w-56 bg-gray-900 text-gray-100 min-h-screen flex flex-col">
      <div className="p-4 border-b border-gray-700">
        <h1 className="text-lg font-bold">OpenEye</h1>
      </div>
      <nav className="flex-1 p-2">
        {navItems.map((item) => {
          const isActive = pathname === item.href;
          return (
            <Link
              key={item.href}
              href={item.href}
              className={`flex items-center gap-3 px-3 py-2 rounded-md text-sm mb-1 transition-colors ${
                isActive
                  ? "bg-gray-700 text-white"
                  : "text-gray-400 hover:bg-gray-800 hover:text-white"
              }`}
            >
              <span>{item.icon}</span>
              <span>{item.label}</span>
            </Link>
          );
        })}
      </nav>
    </aside>
  );
}
