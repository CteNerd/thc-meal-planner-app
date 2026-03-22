import { Stack } from 'aws-cdk-lib';
import { Construct } from 'constructs';
import type { SharedStackProps } from './stack-props';

export class NotificationsStack extends Stack {
  public constructor(scope: Construct, id: string, props: SharedStackProps) {
    super(scope, id, props);

    // Intentionally left as a deploy-time skeleton.
    // Verified SES identities depend on local profile data and AWS account access.
  }
}
