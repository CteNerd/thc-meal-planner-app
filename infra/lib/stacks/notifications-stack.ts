import { Stack } from 'aws-cdk-lib';
import * as budgets from 'aws-cdk-lib/aws-budgets';
import { Construct } from 'constructs';
import type { SharedStackProps } from './stack-props';

export class NotificationsStack extends Stack {
  public constructor(scope: Construct, id: string, props: SharedStackProps) {
    super(scope, id, props);

    const alertEmails = props.deploymentConfig.budgets.alertEmails.filter((email) => email.trim().length > 0);

    if (alertEmails.length === 0) {
      return;
    }

    new budgets.CfnBudget(this, 'MonthlyCostBudget', {
      budget: {
        budgetName: `thc-meal-planner-${props.deploymentConfig.name}-monthly`,
        budgetType: 'COST',
        timeUnit: 'MONTHLY',
        budgetLimit: {
          amount: props.deploymentConfig.budgets.monthlyLimitUsd,
          unit: 'USD'
        }
      },
      notificationsWithSubscribers: [
        {
          notification: {
            comparisonOperator: 'GREATER_THAN',
            notificationType: 'ACTUAL',
            threshold: props.deploymentConfig.budgets.alertThresholdPercent,
            thresholdType: 'PERCENTAGE'
          },
          subscribers: alertEmails.map((address) => ({
            subscriptionType: 'EMAIL',
            address
          }))
        }
      ]
    });
  }
}
