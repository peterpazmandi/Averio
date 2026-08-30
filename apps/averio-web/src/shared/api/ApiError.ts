export interface ProblemDetails {
    type?: string;
    title?: string;
    status?: number;
    detail?: string;
    instance?: string;
    code?: string;
    traceId?: string;
}

export class ApiError extends Error {
    public readonly status: number;
    public readonly code?: string;
    public readonly traceId?: string;

    constructor(
        message: string,
        status: number,
        code?: string,
        traceId?: string,
    ) {
        super(message);

        this.name = 'ApiError';
        this.status = status;
        this.code = code;
        this.traceId = traceId;
    }
}