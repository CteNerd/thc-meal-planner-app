import { Duration } from 'aws-cdk-lib';
import * as lambda from 'aws-cdk-lib/aws-lambda';
import { Construct } from 'constructs';

export type DotnetLambdaFunctionProps = {
  functionName: string;
  memorySize: number;
  timeoutSeconds: number;
  environment?: Record<string, string>;
};

export class DotnetLambdaFunction extends Construct {
  public readonly function: lambda.Function;

  public constructor(scope: Construct, id: string, props: DotnetLambdaFunctionProps) {
    super(scope, id);

    this.function = new lambda.Function(this, 'Resource', {
      functionName: props.functionName,
      runtime: lambda.Runtime.PROVIDED_AL2023,
      architecture: lambda.Architecture.ARM_64,
      handler: 'bootstrap',
      code: lambda.Code.fromAsset('../backend/ThcMealPlanner.Api/bin/Release/net9.0/linux-arm64/publish'),
      memorySize: props.memorySize,
      timeout: Duration.seconds(props.timeoutSeconds),
      environment: props.environment
    });
  }
}
