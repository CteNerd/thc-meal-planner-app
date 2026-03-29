import { CfnOutput, Duration, Stack } from 'aws-cdk-lib';
import * as cognito from 'aws-cdk-lib/aws-cognito';
import { AwsCustomResource, AwsCustomResourcePolicy, PhysicalResourceId } from 'aws-cdk-lib/custom-resources';
import { Construct } from 'constructs';
import type { AuthOutputs, SharedStackProps } from './stack-props';

export class AuthStack extends Stack {
  public readonly outputs: AuthOutputs;

  public constructor(scope: Construct, id: string, props: SharedStackProps) {
    super(scope, id, props);

    const prefix = `thc-meal-planner-${props.deploymentConfig.name}`;
    const allowedOrigins = props.deploymentConfig.domainName
      ? [`https://${props.deploymentConfig.domainName}`]
      : [];

    if (props.deploymentConfig.name === 'dev') {
      allowedOrigins.push('http://localhost:5173', 'http://127.0.0.1:5173');
    }

    const userPool = new cognito.UserPool(this, 'UserPool', {
      userPoolName: `${prefix}-user-pool`,
      signInAliases: { email: true },
      selfSignUpEnabled: false,
      userInvitation: {
        emailSubject: 'THC Meal Planner: Finish Setting Up Your Account',
        emailBody: [
          'Hi there,',
          '',
          'Your THC Meal Planner account is ready.',
          '',
          'Temporary sign-in details:',
          'Username: {username}',
          'Temporary password: {####}',
          '',
          'Next steps:',
          '1) Sign in with the temporary password',
          '2) Create a new password',
          '3) Complete TOTP authenticator setup',
          '',
          'If you were not expecting this email, please reply to the household admin.',
          '',
          'THC Meal Planner'
        ].join('\n')
      },
      mfa: cognito.Mfa.REQUIRED,
      mfaSecondFactor: { sms: false, otp: true },
      passwordPolicy: {
        minLength: 12,
        requireLowercase: true,
        requireUppercase: true,
        requireDigits: true,
        requireSymbols: true
      }
    });

    const userPoolClient = userPool.addClient('WebClient', {
      userPoolClientName: `${prefix}-web-client`,
      authFlows: {
        userSrp: true,
        userPassword: false,
        adminUserPassword: false,
        custom: false
      },
      generateSecret: false,
      accessTokenValidity: Duration.hours(1),
      idTokenValidity: Duration.hours(1),
      refreshTokenValidity: Duration.days(30),
      oAuth: allowedOrigins.length > 0
        ? {
            callbackUrls: allowedOrigins,
            logoutUrls: allowedOrigins,
            flows: {
              authorizationCodeGrant: true
            },
            scopes: [cognito.OAuthScope.OPENID, cognito.OAuthScope.EMAIL, cognito.OAuthScope.PROFILE]
          }
        : undefined,
      supportedIdentityProviders: [cognito.UserPoolClientIdentityProvider.COGNITO]
    });

    const domain = userPool.addDomain('UserPoolDomain', {
      cognitoDomain: {
        domainPrefix: `${prefix}-auth`
      }
    });

    this.outputs = {
      userPoolId: userPool.userPoolId,
      userPoolClientId: userPoolClient.userPoolClientId,
      userPoolDomain: domain.domainName
    };

    // Idempotently ensure custom attributes exist on the pool.
    // Cognito allows adding attributes but not removing them, so CloudFormation
    // cannot manage them via the UserPool resource (a schema change forces pool
    // replacement which would delete all users). An AwsCustomResource calls
    // AddCustomAttributes on every deploy; Cognito ignores attributes that
    // already exist, making this safe to run repeatedly.
    new AwsCustomResource(this, 'AddCustomAttributes', {
      onUpdate: {
        service: 'CognitoIdentityServiceProvider',
        action: 'addCustomAttributes',
        parameters: {
          UserPoolId: userPool.userPoolId,
          CustomAttributes: [
            { Name: 'familyId', AttributeDataType: 'String', Mutable: true, Required: false },
            { Name: 'role',     AttributeDataType: 'String', Mutable: true, Required: false }
          ]
        },
        physicalResourceId: PhysicalResourceId.of(`${prefix}-custom-attrs-v1`),
        // Cognito throws InvalidParameterException when the attributes already
        // exist. Treat that as a no-op so re-deploys are safe on existing pools.
        ignoreErrorCodesMatching: 'InvalidParameterException'
      },
      policy: AwsCustomResourcePolicy.fromSdkCalls({ resources: [userPool.userPoolArn] })
    });

    new CfnOutput(this, 'UserPoolId', { value: this.outputs.userPoolId });
    new CfnOutput(this, 'UserPoolClientId', { value: this.outputs.userPoolClientId });
    new CfnOutput(this, 'UserPoolDomain', { value: this.outputs.userPoolDomain });
  }
}
