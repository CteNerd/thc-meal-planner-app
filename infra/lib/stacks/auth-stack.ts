import { CfnOutput, Duration, Stack } from 'aws-cdk-lib';
import * as cognito from 'aws-cdk-lib/aws-cognito';
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

    new CfnOutput(this, 'UserPoolId', { value: this.outputs.userPoolId });
    new CfnOutput(this, 'UserPoolClientId', { value: this.outputs.userPoolClientId });
    new CfnOutput(this, 'UserPoolDomain', { value: this.outputs.userPoolDomain });
  }
}
