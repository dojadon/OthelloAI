dotnetenv("core");
library = NET.addAssembly('C:\Users\zyand\source\repos\dojadon\OthelloAI\bin\Release\net6.0\OthelloAI.dll');

import OthelloAI.*

n = 100;
sizes = 2:8;
types = ["pext" "scan"];

a = zeros(n, 2, length(sizes));

for j = 1:2
    t = types(j);

    parfor k = 1:n
        net_array = Tester.TestEvaluationTime(10000, 5, sizes, t);
        a(k, j, :) = single(net_array);
    end
end

%% 

b = squeeze(mean(a, 1));
b(1, :)
hold on
plot(sizes, b(1, :));
plot(sizes, b(2, :));
hold off
% ylim([0, 150]);