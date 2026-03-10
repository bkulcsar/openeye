"use client";

import { useEffect, useState } from "react";
import { RuleBuilderDialog } from "@/components/rule-builder/RuleBuilderDialog";
import { RuleFormData } from "@/components/rule-builder/types";

interface Rule {
  id: string;
  name: string;
  cameraId: string;
  objectClass: string;
  zoneId: string | null;
  enabled: boolean;
  conditions: Array<{ type: string; params: Record<string, unknown> }>;
  logic: string;
  cooldown: number;
}

export default function RulesPage() {
  const [rules, setRules] = useState<Rule[]>([]);
  const [showBuilder, setShowBuilder] = useState(false);
  const [editingRule, setEditingRule] = useState<Rule | null>(null);

  useEffect(() => {
    fetch("/api/rules")
      .then((r) => r.json())
      .then(setRules);
  }, []);

  const handleSave = async (data: RuleFormData) => {
    const method = editingRule ? "PUT" : "POST";
    const url = editingRule ? `/api/rules/${editingRule.id}` : "/api/rules";

    const res = await fetch(url, {
      method,
      headers: { "Content-Type": "application/json" },
      body: JSON.stringify(data),
    });

    if (res.ok) {
      const savedRule = await res.json();
      if (editingRule) {
        setRules(rules.map((r) => (r.id === savedRule.id ? savedRule : r)));
      } else {
        setRules([savedRule, ...rules]);
      }
    }

    setShowBuilder(false);
    setEditingRule(null);
  };

  const handleDelete = async (id: string) => {
    await fetch(`/api/rules/${id}`, { method: "DELETE" });
    setRules(rules.filter((r) => r.id !== id));
  };

  return (
    <div className="max-w-4xl mx-auto p-8">
      <div className="flex items-center justify-between mb-6">
        <h1 className="text-2xl font-bold">Rules</h1>
        <button
          onClick={() => setShowBuilder(true)}
          className="px-4 py-2 bg-blue-600 text-white rounded-lg hover:bg-blue-700"
        >
          Create Rule
        </button>
      </div>

      <div className="space-y-3">
        {rules.map((rule) => (
          <div key={rule.id} className="border rounded-lg p-4 bg-white shadow-sm">
            <div className="flex items-center justify-between">
              <div>
                <h3 className="font-medium">{rule.name}</h3>
                <p className="text-sm text-gray-500">
                  {rule.objectClass} · {rule.conditions.length} condition(s) · {rule.logic}
                </p>
              </div>
              <div className="flex gap-2">
                <button
                  onClick={() => { setEditingRule(rule); setShowBuilder(true); }}
                  className="text-sm text-blue-600 hover:text-blue-800"
                >
                  Edit
                </button>
                <button
                  onClick={() => handleDelete(rule.id)}
                  className="text-sm text-red-600 hover:text-red-800"
                >
                  Delete
                </button>
              </div>
            </div>
          </div>
        ))}
        {rules.length === 0 && (
          <p className="text-gray-500 text-center py-8">No rules yet. Create one to get started.</p>
        )}
      </div>

      {showBuilder && (
        <RuleBuilderDialog
          cameraId={editingRule?.cameraId ?? ""}
          initialData={editingRule ? {
            name: editingRule.name,
            objectClass: editingRule.objectClass,
            zoneId: editingRule.zoneId ?? undefined,
            conditions: editingRule.conditions,
            logic: editingRule.logic as "all" | "any",
            cooldown: editingRule.cooldown,
          } : undefined}
          onSave={handleSave}
          onCancel={() => { setShowBuilder(false); setEditingRule(null); }}
        />
      )}
    </div>
  );
}
