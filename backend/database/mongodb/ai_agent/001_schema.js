const db = db.getSiblingDB("ai_agent_db");

const collections = [
    "conversations",
    "messages",
    "agent_runs",
    "agent_plans",
    "agent_steps",
    "tool_calls",
    "agent_memory",
    "ai_actions",
    "model_responses",
    "llm_providers",
    "llm_models",
    "llm_routing_rules",
    "llm_usage",
    "prompt_templates",
    "prompt_versions",
    "execution_context",
    "chat_history_consent"
];

for (const collectionName of collections) {
    if (!db.getCollectionNames().includes(collectionName)) {
        db.createCollection(collectionName);
    }
}






db.conversations.createIndex(
    { tenantId: 1, userId: 1, createdAt: -1 }
);

db.conversations.createIndex(
    { tenantId: 1, status: 1 }
);






db.conversations.createIndex(
    { tenantId: 1, userId: 1, updatedAt: -1 },
    { name: "ix_history_user_updated" }
);


db.conversations.createIndex(
    { expiresAt: 1 },
    { name: "ix_history_ttl", expireAfterSeconds: 0 }
);









db.messages.createIndex(
    { tenantId: 1, conversationId: 1, createdAt: 1 }
);

db.messages.createIndex(
    { tenantId: 1, role: 1, createdAt: -1 }
);






db.agent_runs.createIndex(
    { tenantId: 1, createdAt: -1 }
);

db.agent_runs.createIndex(
    { tenantId: 1, conversationId: 1, createdAt: -1 }
);

db.agent_runs.createIndex(
    { tenantId: 1, status: 1, createdAt: -1 }
);

db.agent_runs.createIndex(
    { tenantId: 1, runId: 1 },
    { unique: true }
);






db.agent_plans.createIndex(
    { tenantId: 1, runId: 1, version: 1 },
    { unique: true }
);

db.agent_plans.createIndex(
    { tenantId: 1, runId: 1, createdAt: -1 }
);






db.agent_steps.createIndex(
    { tenantId: 1, runId: 1, stepNumber: 1 }
);

db.agent_steps.createIndex(
    { tenantId: 1, status: 1, createdAt: -1 }
);






db.tool_calls.createIndex(
    { tenantId: 1, runId: 1, createdAt: 1 }
);

db.tool_calls.createIndex(
    { tenantId: 1, toolName: 1, createdAt: -1 }
);

db.tool_calls.createIndex(
    { tenantId: 1, status: 1, createdAt: -1 }
);






db.agent_memory.createIndex(
    { tenantId: 1, userId: 1, memoryType: 1 }
);

db.agent_memory.createIndex(
    { tenantId: 1, key: 1 }
);

db.agent_memory.createIndex(
    { expiresAt: 1 },
    { expireAfterSeconds: 0 }
);






db.ai_actions.createIndex(
    { tenantId: 1, createdAt: -1 }
);

db.ai_actions.createIndex(
    { tenantId: 1, runId: 1, createdAt: 1 }
);

db.ai_actions.createIndex(
    { tenantId: 1, status: 1, createdAt: -1 }
);






db.model_responses.createIndex(
    { tenantId: 1, runId: 1, createdAt: 1 }
);

db.model_responses.createIndex(
    { tenantId: 1, provider: 1, model: 1, createdAt: -1 }
);






db.llm_providers.createIndex(
    { tenantId: 1, code: 1 },
    { unique: true }
);

db.llm_providers.createIndex(
    { tenantId: 1, enabled: 1 }
);






db.llm_models.createIndex(
    { tenantId: 1, providerCode: 1, modelCode: 1 },
    { unique: true }
);

db.llm_models.createIndex(
    { tenantId: 1, enabled: 1 }
);






db.llm_routing_rules.createIndex(
    { tenantId: 1, taskType: 1, priority: 1 },
    { unique: true }
);

db.llm_routing_rules.createIndex(
    { tenantId: 1, enabled: 1, priority: 1 }
);






db.llm_usage.createIndex(
    { tenantId: 1, createdAt: -1 }
);

db.llm_usage.createIndex(
    { tenantId: 1, userId: 1, createdAt: -1 }
);

db.llm_usage.createIndex(
    { tenantId: 1, providerCode: 1, modelCode: 1, createdAt: -1 }
);

db.llm_usage.createIndex(
    { tenantId: 1, runId: 1 }
);






db.prompt_templates.createIndex(
    { tenantId: 1, code: 1 },
    { unique: true }
);

db.prompt_templates.createIndex(
    { tenantId: 1, active: 1 }
);






db.prompt_versions.createIndex(
    { tenantId: 1, templateCode: 1, version: 1 },
    { unique: true }
);

db.prompt_versions.createIndex(
    { tenantId: 1, templateCode: 1, createdAt: -1 }
);






db.execution_context.createIndex(
    { tenantId: 1, runId: 1 },
    { unique: true }
);

db.execution_context.createIndex(
    { tenantId: 1, correlationId: 1 }
);

db.execution_context.createIndex(
    { tenantId: 1, updatedAt: -1 }
);

print("ai_agent_db schema/indexes created successfully.");