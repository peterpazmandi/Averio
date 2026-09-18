import { useState } from 'react';
import { useQuery } from '@tanstack/react-query';
import { useTranslation } from 'react-i18next';
import { AverioBrand } from '../../../shared/components/branding/AverioBrand';
import { ConceptPreferences } from '../../../shared/components/concept/ConceptPreferences';
import { ConceptIcon } from '../../../shared/components/concept/ConceptIcon';
import { onboardingQueries } from '../api/onboardingQueries';
import { OnboardingCompleted } from './OnboardingCompleted';
import { OnboardingForm } from './OnboardingForm';
import type { OnboardingStep } from './OnboardingForm';
import '../../../shared/styles/concept.css';

const steps = ['profile', 'household', 'firstAccount'] as const;

export function OnboardingForUser({ userId }: { userId: string }) {
    const { t } = useTranslation();
    const statusQuery = useQuery(onboardingQueries.status(userId));
    const status = statusQuery.data;
    const [viewedStep, setViewedStep] = useState<OnboardingStep | null>(null);
    const currentStep = viewedStep ?? status?.currentStep;
    const currentIndex = status?.completed ? steps.length
        : steps.findIndex((step) => step === currentStep);

    return (
        <main className="av-ui av-onboarding">
            <header className="av-onboarding-top">
                <div className="av-brand"><AverioBrand /></div>
                <ConceptPreferences />
            </header>

            <div className="av-onboarding-layout">
                <section className="av-onboarding-intro" aria-labelledby="onboarding-title">
                    <span className="av-badge av-green">{t('onboarding.eyebrow')}</span>
                    <h1 id="onboarding-title">{t('onboarding.title')}</h1>
                    <p>{t('onboarding.description')}</p>

                    <ol className="av-steps" aria-label={t('onboarding.title')}>
                        {steps.map((step, index) => (
                            <li key={step}
                                className={'av-step' + (index < currentIndex ? ' av-done' : '')}
                                aria-current={index === currentIndex ? 'step' : undefined}>
                                <span className="av-step-number" aria-hidden="true">
                                    {index < currentIndex ? '✓' : index + 1}
                                </span>
                                <span>{t('onboarding.steps.' + step)}</span>
                            </li>
                        ))}
                    </ol>

                    <div className="av-hero">
                        <span className="av-hero-icon"><ConceptIcon name="household" /></span>
                        <div>
                            <h3>{t('onboarding.householdOfOne')}</h3>
                            <p>{t('concept.onboardingNote')}</p>
                        </div>
                    </div>
                </section>

                <section className="av-card av-onboarding-panel"
                    aria-label={t('onboarding.title')}>
                    {status?.completed ? (
                        <OnboardingCompleted key={userId} userId={userId} />
                    ) : status ? (
                        <OnboardingForm
                            key={userId}
                            userId={userId}
                            initialData={status}
                            currentStep={currentStep ?? status.currentStep}
                            onStepChange={setViewedStep}
                        />
                    ) : statusQuery.isError ? (
                        <div className="space-y-4">
                            <p role="alert" className="av-error">{t('errors.genericDescription')}</p>
                            <button type="button" className="av-btn av-primary"
                                disabled={statusQuery.isFetching}
                                onClick={() => { void statusQuery.refetch(); }}>
                                {t('common.actions.retry')}
                            </button>
                        </div>
                    ) : (
                        <p role="status">{t('common.status.loading')}</p>
                    )}
                </section>
            </div>
        </main>
    );
}