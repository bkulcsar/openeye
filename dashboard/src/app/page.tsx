import Link from "next/link";

const navItems = [
  { href: "/cameras", title: "Cameras", description: "Manage camera streams and settings" },
  { href: "/rules", title: "Rules", description: "Configure detection rules and conditions" },
  { href: "/events", title: "Events", description: "View detected events and alerts" },
];

export default function Home() {
  return (
    <main className="flex min-h-screen flex-col items-center justify-center p-24">
      <h1 className="text-4xl font-bold">OpenEye Dashboard</h1>
      <p className="mt-4 text-gray-600">Video analytics monitoring and configuration</p>
      <div className="mt-10 grid grid-cols-1 sm:grid-cols-3 gap-4">
        {navItems.map((item) => (
          <Link
            key={item.href}
            href={item.href}
            className="border rounded-lg p-6 bg-white shadow-sm hover:shadow-md transition-shadow text-center"
          >
            <h2 className="text-lg font-semibold">{item.title}</h2>
            <p className="text-sm text-gray-500 mt-1">{item.description}</p>
          </Link>
        ))}
      </div>
    </main>
  );
}
