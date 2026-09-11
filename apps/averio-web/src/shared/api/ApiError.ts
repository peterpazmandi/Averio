export interface ProblemDetails {
    type?: string;
    title?: string;
    status?: number;
    detail?: string;
    instance?: string;
    code?: string;
    traceId?: string;

    errors?: Record<string, string[]>;
}

export class ApiError extends Error {
    public readonly status: number;
    public readonly code?: string;
    public readonly traceId?: string;
    public readonly validationErrors?: Record<string, string[]>;

    constructor(
        message: string,
        status: number,
        code?: string,
        traceId?: string,
        validationErrors?: Record<string, string[]>,
    ) {
        super(message);

        this.name = 'ApiError';
        this.status = status;
        this.code = code;
        this.traceId = traceId;
        this.validationErrors = validationErrors;
    }
}