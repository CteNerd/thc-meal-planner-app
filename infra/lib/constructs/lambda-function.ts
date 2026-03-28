import { Duration } from 'aws-cdk-lib';
import * as lambda from 'aws-cdk-lib/aws-lambda';
import { Construct } from 'constructs';
import * as fs from 'node:fs';
import * as path from 'node:path';

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

    const publishPath = path.resolve(
      __dirname,
      '../../../backend/ThcMealPlanner.Api/bin/Release/net9.0/linux-arm64/publish'
    );
    const bootstrapPath = path.join(publishPath, 'bootstrap');

    if (!fs.existsSync(bootstrapPath)) {
      // Lambda custom runtime requires an executable bootstrap at package root.
      fs.writeFileSync(bootstrapPath, '#!/bin/sh\nset -e\n./ThcMealPlanner.Api\n', { encoding: 'utf8' });
      fs.chmodSync(bootstrapPath, 0o755);
    }

    this.function = new lambda.Function(this, 'Resource', {
      functionName: props.functionName,
      runtime: lambda.Runtime.PROVIDED_AL2023,
      architecture: lambda.Architecture.ARM_64,
      handler: 'bootstrap',
      code: lambda.Code.fromAsset(publishPath),
      memorySize: props.memorySize,
      timeout: Duration.seconds(props.timeoutSeconds),
      environment: props.environment
    });
  }
}
